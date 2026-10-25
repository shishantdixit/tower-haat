using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Notifications;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Orders;

/// <summary>Builds order responses and expires overdue orders before they are shown.</summary>
public sealed class OrderViews(IAppDbContext db, OrderNotifications notifications, TimeProvider time)
{
    /// <summary>
    /// Expires every Placed order of the current society that waited too long. Runs before orders are
    /// read, so expiry never depends on the background job having run (IIS may have unloaded the app).
    /// </summary>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var cutOff = now - Order.AcceptWithin;
        var due = await db.Orders
            .Include(o => o.Payment)
            .Where(o => o.Status == OrderStatus.Placed && o.PlacedAt <= cutOff)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        due.ForEach(o => o.Expire(now));
        await db.SaveChangesAsync(cancellationToken);

        foreach (var order in due)
        {
            await notifications.StatusChangedAsync(order, cancellationToken);
        }

        return due.Count;
    }

    /// <summary>Reminds owners once about orders that have waited 10 minutes (run by the job, not on reads).</summary>
    public async Task<int> RemindDueAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var since = now - Order.RemindAfter;
        var due = (await db.Orders
                .Include(o => o.Items)
                .Include(o => o.Payment)
                .Where(o => o.Status == OrderStatus.Placed && o.ReminderSentAt == null && o.PlacedAt <= since)
                .ToListAsync(cancellationToken))
            .Where(o => o.IsDueForReminder(now))
            .ToList();

        if (due.Count == 0)
        {
            return 0;
        }

        due.ForEach(o => o.MarkReminded(now));
        await db.SaveChangesAsync(cancellationToken);

        foreach (var order in due)
        {
            await notifications.ReminderAsync(order, cancellationToken);
        }

        return due.Count;
    }

    public async Task<OrderResponse> ToResponseAsync(Order order, CancellationToken cancellationToken)
    {
        var business = await db.Businesses
            .Where(b => b.Id == order.BusinessId)
            .Select(b => new { b.Name, b.WhatsAppPhone, b.UpiId })
            .SingleAsync(cancellationToken);
        var history = order.History.Count > 0
            ? order.History.ToList()
            : await db.OrderStatusChanges.Where(h => h.OrderId == order.Id).ToListAsync(cancellationToken);

        return new OrderResponse(
            order.Id,
            order.OrderNo,
            order.BusinessId,
            business.Name,
            $"https://wa.me/{business.WhatsAppPhone.TrimStart('+')}",
            order.Status,
            order.PlacedAt,
            ExpiresAt(order),
            order.PreferredTime,
            order.Note,
            order.CustomerName,
            order.CustomerPhone,
            order.Tower,
            order.Flat,
            order.Items.Select(i => new OrderLineResponse(i.NameSnapshot, i.PriceSnapshot, i.Quantity, i.IsVegSnapshot, i.LineTotal)).ToList(),
            order.Subtotal,
            order.DeliveryCharge,
            order.Total,
            order.PaymentMethod,
            Payment(order, business.Name, business.UpiId),
            history.OrderBy(h => h.At).Select(h => new OrderStepResponse(h.Status, h.Reason, h.At)).ToList());
    }

    public async Task<List<OrderSummaryResponse>> ToSummariesAsync(List<Order> orders, CancellationToken cancellationToken)
    {
        var businessIds = orders.Select(o => o.BusinessId).Distinct().ToList();
        var names = await db.Businesses
            .Where(b => businessIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        return orders
            .Select(o => new OrderSummaryResponse(
                o.Id,
                o.OrderNo,
                o.BusinessId,
                names.GetValueOrDefault(o.BusinessId, string.Empty),
                o.Status,
                o.PlacedAt,
                ExpiresAt(o),
                o.PreferredTime,
                o.CustomerName,
                o.Tower,
                o.Flat,
                string.Join(", ", o.Items.Select(i => $"{i.Quantity} × {i.NameSnapshot}")),
                o.Total,
                o.PaymentMethod,
                o.Payment.Status))
            .ToList();
    }

    private static PaymentResponse Payment(Order order, string businessName, string? upiId)
    {
        var payment = order.Payment;
        var upi = order.IsUpiPayable && upiId is not null
            ? new UpiPaymentResponse(upiId, businessName, order.Total, UpiLink.Build(upiId, businessName, order.Total, $"TowerHaat order #{order.OrderNo}"))
            : null;

        return new PaymentResponse(payment.Status, payment.Utr, payment.MarkedPaidAt, payment.SettledAt, payment.RefundedAt, payment.Note, upi);
    }

    private static DateTime? ExpiresAt(Order order) =>
        order.Status == OrderStatus.Placed ? order.PlacedAt + Order.AcceptWithin : null;
}
