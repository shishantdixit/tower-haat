using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Common;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Orders;

/// <summary>The customer's side of ordering: checkout (C-06), their orders (C-07) and cancelling (C-09).</summary>
public sealed class OrderService(IAppDbContext db, ICurrentSociety society, OrderViews views, TimeProvider time)
{
    public const int MaxNoteLength = 300;
    public const int MaxLines = 30;
    public static readonly TimeSpan MinLeadTime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Places an order after checking every rule: the business is active and taking orders now,
    /// items are on its menu and in stock, the minimum order is met. The same idempotency key
    /// returns the order already placed instead of a second one.
    /// </summary>
    public async Task<OrderResponse> PlaceAsync(Guid userId, PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        if (society.SocietyId is null)
        {
            throw new AppException(ErrorKind.Forbidden, "Finish signing up before you order.");
        }

        if (await FindByIdempotencyKeyAsync(userId, request.IdempotencyKey, cancellationToken) is { } existing)
        {
            return await views.ToResponseAsync(existing, cancellationToken);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var business = await db.Businesses.SingleOrDefaultAsync(b => b.Id == request.BusinessId, cancellationToken);
        var type = business is null
            ? null
            : await db.BusinessTypes.SingleAsync(t => t.Id == business.BusinessTypeId, cancellationToken);

        if (business is null || business.Status != BusinessStatus.Active || type is null || !type.OrderingEnabled)
        {
            throw new AppException(ErrorKind.Validation, "This business is not taking orders.");
        }

        if (business.OwnerUserId == userId)
        {
            throw new AppException(ErrorKind.Validation, "You cannot order from your own business.");
        }

        var localNow = IndiaTime.TimeNow(time);
        if (!business.IsOpen || !business.IsOpenAt(localNow))
        {
            throw new AppException(ErrorKind.Validation, $"{business.Name} is closed right now.");
        }

        if (!business.Hours.AcceptsOrdersAt(localNow))
        {
            throw new AppException(ErrorKind.Validation, $"{business.Name} has stopped taking orders for today.");
        }

        var lines = await LoadLinesAsync(business, request.Items, cancellationToken);
        var subtotal = lines.Sum(l => l.Item.Price * l.Quantity);
        if (subtotal < business.MinOrderValue)
        {
            throw new AppException(
                ErrorKind.Validation,
                $"The minimum order is ₹{business.MinOrderValue:0.##}. Add ₹{business.MinOrderValue - subtotal:0.##} more.");
        }

        if (request.PaymentMethod == PaymentMethod.Upi && business.UpiId is null)
        {
            throw new AppException(ErrorKind.Validation, $"{business.Name} takes cash on delivery only.");
        }

        var preferredTime = PreferredTime(business, request.PreferredDay, request.PreferredTime);
        var customer = await CustomerAsync(userId, cancellationToken);
        var note = Validate.Optional(request.Note, "Note", MaxNoteLength);

        // Numbers are sequential per business; a clash with a simultaneous order just retries.
        for (var attempt = 1; ; attempt++)
        {
            var orderNo = (await db.Orders.Where(o => o.BusinessId == business.Id).MaxAsync(o => (int?)o.OrderNo, cancellationToken) ?? 0) + 1;
            var order = new Order(business, userId, orderNo, customer, lines, preferredTime, note, request.PaymentMethod, request.IdempotencyKey, now);
            db.Orders.Add(order);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return await views.ToResponseAsync(order, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.DiscardPendingChanges();
                if (await FindByIdempotencyKeyAsync(userId, request.IdempotencyKey, cancellationToken) is { } placed)
                {
                    return await views.ToResponseAsync(placed, cancellationToken);
                }
            }
        }
    }

    public async Task<List<OrderSummaryResponse>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        var orders = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Where(o => o.CustomerUserId == userId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return await views.ToSummariesAsync(orders, cancellationToken);
    }

    public async Task<OrderResponse> GetMineAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        return await views.ToResponseAsync(await FindMineAsync(userId, orderId, cancellationToken), cancellationToken);
    }

    /// <summary>C-09: only while the business has not accepted it yet.</summary>
    public async Task<OrderResponse> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        var order = await FindMineAsync(userId, orderId, cancellationToken);

        try
        {
            order.CancelByCustomer(userId, time.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException e)
        {
            throw new AppException(ErrorKind.Conflict, e.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await views.ToResponseAsync(order, cancellationToken);
    }

    /// <summary>C-08: "I've paid", with the UPI transaction reference if the customer has it.</summary>
    public async Task<OrderResponse> MarkPaidAsync(Guid userId, Guid orderId, MarkPaidRequest request, CancellationToken cancellationToken)
    {
        await views.ExpireDueAsync(cancellationToken);
        var order = await FindMineAsync(userId, orderId, cancellationToken);
        var utr = Validate.Utr(request.Utr);

        try
        {
            order.MarkPaidByCustomer(utr, time.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException e)
        {
            throw new AppException(ErrorKind.Conflict, e.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await views.ToResponseAsync(order, cancellationToken);
    }

    private async Task<Order> FindMineAsync(Guid userId, Guid orderId, CancellationToken cancellationToken) =>
        await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.History)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerUserId == userId, cancellationToken)
        ?? throw new AppException(ErrorKind.NotFound, "Order not found.");

    private Task<Order?> FindByIdempotencyKeyAsync(Guid userId, Guid key, CancellationToken cancellationToken) =>
        db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .Include(o => o.History)
            .SingleOrDefaultAsync(o => o.CustomerUserId == userId && o.IdempotencyKey == key, cancellationToken);

    /// <summary>Items must be on the business's visible menu, available today and within the daily limit.</summary>
    private async Task<List<(CatalogItem Item, int Quantity)>> LoadLinesAsync(
        Business business, IReadOnlyList<OrderLineRequest>? requested, CancellationToken cancellationToken)
    {
        var quantities = (requested ?? [])
            .GroupBy(l => l.ItemId)
            .Select(g => (ItemId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .ToList();

        if (quantities.Count == 0)
        {
            throw new AppException(ErrorKind.Validation, "Your cart is empty.");
        }

        if (quantities.Count > MaxLines || quantities.Any(q => q.Quantity is < 1 or > OrderItem.MaxQuantity))
        {
            throw new AppException(ErrorKind.Validation, $"Each item can be ordered 1 to {OrderItem.MaxQuantity} at a time.");
        }

        var ids = quantities.Select(q => q.ItemId).ToList();
        var items = await db.CatalogItems
            .Where(i => i.BusinessId == business.Id && ids.Contains(i.Id) && !i.IsHidden)
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var sold = await SoldToday.ForBusinessAsync(db, business.Id, time, cancellationToken);

        var lines = new List<(CatalogItem, int)>();
        foreach (var (itemId, quantity) in quantities)
        {
            if (!items.TryGetValue(itemId, out var item))
            {
                throw new AppException(ErrorKind.Validation, "An item in your cart is no longer on the menu. Please remove it.");
            }

            var soldToday = sold.GetValueOrDefault(item.Id);
            if (item.IsSoldOut(soldToday))
            {
                throw new AppException(ErrorKind.Validation, $"{item.Name} is sold out. Please remove it from your cart.");
            }

            if (item.RemainingToday(soldToday) is { } left && quantity > left)
            {
                throw new AppException(ErrorKind.Validation, $"Only {left} {item.Name} left today.");
            }

            lines.Add((item, quantity));
        }

        return lines;
    }

    /// <summary>Later today or tomorrow, within the business's hours and at least 15 minutes ahead.</summary>
    private DateTime? PreferredTime(Business business, string? day, string? at)
    {
        if (string.IsNullOrWhiteSpace(day))
        {
            return null;
        }

        var today = IndiaTime.Today(time);
        var date = day.Trim().ToLowerInvariant() switch
        {
            "today" => today,
            "tomorrow" => today.AddDays(1),
            _ => throw new AppException(ErrorKind.Validation, "Choose today or tomorrow."),
        };

        if (!TimeOnly.TryParseExact(at?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock))
        {
            throw new AppException(ErrorKind.Validation, "Choose a delivery time.");
        }

        if (!business.Hours.Contains(clock))
        {
            throw new AppException(ErrorKind.Validation, $"{business.Name} delivers only during its opening hours.");
        }

        var utc = IndiaTime.ToUtc(date, clock);
        if (utc < time.GetUtcNow().UtcDateTime + MinLeadTime)
        {
            throw new AppException(ErrorKind.Validation, "Choose a time at least 15 minutes from now.");
        }

        return utc;
    }

    /// <summary>The customer's name, phone and flat, copied into the order.</summary>
    private async Task<OrderCustomer> CustomerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var profile = await db.ResidentProfiles
            .Include(p => p.Flat!).ThenInclude(f => f.Tower)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new AppException(ErrorKind.Forbidden, "Finish signing up before you order.");

        return new OrderCustomer(
            user.Name ?? string.Empty,
            user.Phone,
            profile.Flat?.Tower.Name ?? profile.ManualTower ?? string.Empty,
            profile.Flat?.Number ?? profile.ManualFlat ?? string.Empty);
    }
}
