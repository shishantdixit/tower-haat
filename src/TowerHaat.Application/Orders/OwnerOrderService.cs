using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Notifications;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Orders;

/// <summary>
/// The business's side of ordering: dashboard (B-05), status actions (B-06) and prep summary (B-07).
/// An owner only ever sees orders placed with their own business, so the customer's phone and flat
/// stay private to the business serving that order.
/// </summary>
public sealed class OwnerOrderService(IAppDbContext db, OrderViews views, OrderNotifications notifications, TimeProvider time)
{
    public const int MaxReasonLength = 300;

    public async Task<OwnerOrdersResponse> ListAsync(Guid userId, OwnerOrderTab tab, CancellationToken cancellationToken)
    {
        var business = await MyBusinessAsync(userId, cancellationToken);
        await views.ExpireDueAsync(cancellationToken);

        var doneSince = IndiaTime.StartOfTodayUtc(time).AddDays(-1);
        var mine = db.Orders.Where(o => o.BusinessId == business.Id);
        var open = await mine
            .Where(o => OrderStatuses.Open.Contains(o.Status))
            .Select(o => o.Status)
            .ToListAsync(cancellationToken);
        var done = await mine.CountAsync(o => !OrderStatuses.Open.Contains(o.Status) && o.StatusChangedAt >= doneSince, cancellationToken);

        var counts = new Dictionary<OwnerOrderTab, int>
        {
            [OwnerOrderTab.New] = open.Count(s => s == OrderStatus.Placed),
            [OwnerOrderTab.Preparing] = open.Count(s => s is OrderStatus.Accepted or OrderStatus.Preparing),
            [OwnerOrderTab.OutForDelivery] = open.Count(s => s == OrderStatus.OutForDelivery),
            [OwnerOrderTab.Done] = done,
        };

        var query = mine.Include(o => o.Items)
            .Include(o => o.Payment).AsQueryable();
        query = tab switch
        {
            OwnerOrderTab.New => query.Where(o => o.Status == OrderStatus.Placed),
            OwnerOrderTab.Preparing => query.Where(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Preparing),
            OwnerOrderTab.OutForDelivery => query.Where(o => o.Status == OrderStatus.OutForDelivery),
            _ => query.Where(o => !OrderStatuses.Open.Contains(o.Status) && o.StatusChangedAt >= doneSince),
        };

        // Open orders oldest first, so nothing waits; finished ones newest first.
        var orders = tab == OwnerOrderTab.Done
            ? await query.OrderByDescending(o => o.StatusChangedAt).Take(100).ToListAsync(cancellationToken)
            : await query.OrderBy(o => o.PlacedAt).ToListAsync(cancellationToken);

        return new OwnerOrdersResponse(counts, await views.ToSummariesAsync(orders, cancellationToken));
    }

    public async Task<OrderResponse> GetAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        return await views.ToResponseAsync(await FindAsync(userId, orderId, cancellationToken), cancellationToken);
    }

    public Task<OrderResponse> AcceptAsync(Guid userId, Guid orderId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, orderId, (o, now) => o.Accept(userId, now), notifications.StatusChangedAsync, cancellationToken);

    public Task<OrderResponse> RejectAsync(Guid userId, Guid orderId, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(userId, orderId, (o, now) => o.Reject(userId, Reason(request), now), notifications.StatusChangedAsync, cancellationToken);

    public Task<OrderResponse> AdvanceAsync(Guid userId, Guid orderId, CancellationToken cancellationToken) =>
        ChangeAsync(userId, orderId, (o, now) => o.Advance(userId, now), notifications.StatusChangedAsync, cancellationToken);

    public Task<OrderResponse> CancelAsync(Guid userId, Guid orderId, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(userId, orderId, (o, now) => o.CancelByOwner(userId, Reason(request), now), notifications.StatusChangedAsync, cancellationToken);

    /// <summary>B-08: UPI received, cash collected, disputed or refunded.</summary>
    public Task<OrderResponse> UpdatePaymentAsync(Guid userId, Guid orderId, PaymentActionRequest request, CancellationToken cancellationToken)
    {
        var note = Validate.Optional(request.Note, "Note", MaxReasonLength);
        return ChangeAsync(userId, orderId, (o, now) =>
        {
            switch (request.Action)
            {
                case PaymentAction.Confirm:
                    o.ConfirmUpiReceived(now);
                    break;
                case PaymentAction.Collect:
                    o.MarkCashCollected(now);
                    break;
                case PaymentAction.Dispute:
                    o.DisputePayment(note, now);
                    break;
                case PaymentAction.Refund:
                    o.MarkRefunded(now);
                    break;
                default:
                    throw new AppException(ErrorKind.Validation, "Unknown payment action.");
            }
        }, notifications.PaymentChangedAsync, cancellationToken);
    }

    /// <summary>
    /// Item-wise totals for today's open orders (B-07): what is accepted and still to be made, and
    /// what is waiting to be accepted. Orders for tomorrow are left out.
    /// </summary>
    public async Task<PrepSummaryResponse> PrepSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var business = await MyBusinessAsync(userId, cancellationToken);
        await views.ExpireDueAsync(cancellationToken);

        var endOfToday = IndiaTime.StartOfTodayUtc(time).AddDays(1);
        var orders = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Where(o => o.BusinessId == business.Id
                && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Preparing)
                && (o.PreferredTime == null || o.PreferredTime < endOfToday))
            .ToListAsync(cancellationToken);

        var lines = orders
            .SelectMany(o => o.Items.Select(i => (o.Status, Item: i)))
            .GroupBy(x => x.Item.NameSnapshot)
            .Select(g => new PrepLine(
                g.Key,
                g.First().Item.IsVegSnapshot,
                g.Where(x => x.Status != OrderStatus.Placed).Sum(x => x.Item.Quantity),
                g.Where(x => x.Status == OrderStatus.Placed).Sum(x => x.Item.Quantity)))
            .OrderByDescending(l => l.ToPrepare)
            .ThenBy(l => l.Name)
            .ToList();

        return new PrepSummaryResponse(
            orders.Count(o => o.Status != OrderStatus.Placed),
            orders.Count(o => o.Status == OrderStatus.Placed),
            lines);
    }

    private async Task<OrderResponse> ChangeAsync(
        Guid userId,
        Guid orderId,
        Action<Order, DateTime> change,
        Func<Order, CancellationToken, Task> notify,
        CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        var order = await FindAsync(userId, orderId, cancellationToken);

        try
        {
            change(order, time.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException e)
        {
            throw new AppException(ErrorKind.Conflict, e.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        await notify(order, cancellationToken);
        return await views.ToResponseAsync(order, cancellationToken);
    }

    private async Task<Business> MyBusinessAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Businesses.SingleOrDefaultAsync(b => b.OwnerUserId == userId, cancellationToken)
        ?? throw new AppException(ErrorKind.NotFound, "You have not registered a business yet.");

    /// <summary>Only orders placed with the owner's own business.</summary>
    private async Task<Order> FindAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        var business = await MyBusinessAsync(userId, cancellationToken);
        return await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.History)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.BusinessId == business.Id, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Order not found.");
    }

    private static string Reason(ReasonRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new AppException(ErrorKind.Validation, "Tell the customer why.");
        }

        return reason.Length <= MaxReasonLength
            ? reason
            : throw new AppException(ErrorKind.Validation, $"The reason can be at most {MaxReasonLength} characters.");
    }
}
