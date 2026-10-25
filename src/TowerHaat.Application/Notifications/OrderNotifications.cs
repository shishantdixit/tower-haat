using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Notifications;

/// <summary>
/// What owners and customers hear about their orders: a new order (B-05), every status change
/// (C-07), payment claims and disputes, and the reminder before expiry. Push text goes through
/// Google's and Apple's servers encrypted, and carries no phone numbers.
/// </summary>
public sealed class OrderNotifications(IAppDbContext db, INotificationQueue queue)
{
    public async Task NewOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(order, cancellationToken);
        queue.Enqueue(ToOwner(order, owner.Id,
            $"New order #{order.OrderNo} · {Rupees(order.Total)}",
            $"{Items(order)} for {order.Tower}-{order.Flat}. Accept within 30 minutes.",
            isNew: true));
    }

    public async Task ReminderAsync(Order order, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(order, cancellationToken);
        var left = (int)Math.Ceiling((order.PlacedAt + Order.AcceptWithin - order.ReminderSentAt!.Value).TotalMinutes);
        queue.Enqueue(ToOwner(order, owner.Id,
            $"Order #{order.OrderNo} is waiting",
            $"Accept it within {left} minutes or it is cancelled automatically.",
            isNew: false));
    }

    /// <summary>Tells the other side about a change: the customer for the owner's actions and expiry, the owner for the customer's.</summary>
    public async Task StatusChangedAsync(Order order, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(order, cancellationToken);
        var reason = order.History.Count > 0 ? order.History[^1].Reason : null;
        var no = order.OrderNo;

        switch (order.Status)
        {
            case OrderStatus.Accepted:
                queue.Enqueue(ToCustomer(order, $"{owner.Name} accepted order #{no}",
                    order.IsUpiPayable ? $"Pay {Rupees(order.Total)} by UPI now, or when it arrives." : "We'll tell you when it is on the way."));
                break;
            case OrderStatus.Preparing:
                queue.Enqueue(ToCustomer(order, $"Order #{no} is being prepared", $"{owner.Name} has started on it."));
                break;
            case OrderStatus.OutForDelivery:
                queue.Enqueue(ToCustomer(order, $"Order #{no} is on the way", $"From {owner.Name} to {order.Tower}-{order.Flat}."));
                break;
            case OrderStatus.Delivered:
                queue.Enqueue(ToCustomer(order, $"Order #{no} delivered", "Enjoy!"));
                break;
            case OrderStatus.Rejected:
                queue.Enqueue(ToCustomer(order, $"{owner.Name} could not take order #{no}", reason ?? "Please try again later."));
                break;
            case OrderStatus.CancelledByOwner:
                queue.Enqueue(ToCustomer(order, $"{owner.Name} cancelled order #{no}", reason ?? string.Empty));
                break;
            case OrderStatus.Expired:
                queue.Enqueue(ToCustomer(order, $"Order #{no} was not accepted",
                    $"{owner.Name} did not respond within 30 minutes, so it was cancelled. Nothing to pay."));
                queue.Enqueue(ToOwner(order, owner.Id, $"Order #{no} expired", "It was not accepted within 30 minutes.", isNew: false));
                break;
            case OrderStatus.CancelledByCustomer:
                queue.Enqueue(ToOwner(order, owner.Id, $"Order #{no} cancelled", $"{order.CustomerName} cancelled it before you accepted.", isNew: false));
                break;
        }
    }

    /// <summary>Payment news: the owner hears about a claim, the customer about a dispute or confirmation.</summary>
    public async Task PaymentChangedAsync(Order order, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(order, cancellationToken);
        var no = order.OrderNo;

        switch (order.Payment.Status)
        {
            case PaymentStatus.CustomerMarkedPaid:
                queue.Enqueue(ToOwner(order, owner.Id, $"{order.CustomerName} paid {Rupees(order.Total)} by UPI",
                    $"Order #{no}. Check your UPI app and confirm.", isNew: false));
                break;
            case PaymentStatus.Disputed:
                queue.Enqueue(ToCustomer(order, $"{owner.Name} has not received your payment",
                    order.Payment.Note ?? $"Order #{no}. Please check your UPI app and try again."));
                break;
            case PaymentStatus.Confirmed:
                queue.Enqueue(ToCustomer(order, $"Payment received for order #{no}", $"{owner.Name} confirmed {Rupees(order.Total)}."));
                break;
            case PaymentStatus.Refunded:
                queue.Enqueue(ToCustomer(order, $"Order #{no} refunded", $"{owner.Name} says they returned {Rupees(order.Total)}."));
                break;
        }
    }

    private async Task<(Guid Id, string Name)> OwnerAsync(Order order, CancellationToken cancellationToken)
    {
        var business = await db.Businesses
            .Where(b => b.Id == order.BusinessId)
            .Select(b => new { b.OwnerUserId, b.Name })
            .SingleAsync(cancellationToken);
        return (business.OwnerUserId, business.Name);
    }

    private static UserNotification ToOwner(Order order, Guid ownerId, string title, string body, bool isNew) =>
        new(ownerId, title, body, $"/owner/orders/{order.Id}", $"order-{order.Id}", Event(order, isNew));

    private static UserNotification ToCustomer(Order order, string title, string body) =>
        new(order.CustomerUserId, title, body, $"/customer/orders/{order.Id}", $"order-{order.Id}", Event(order, isNew: false));

    private static OrderChangedEvent Event(Order order, bool isNew) =>
        new(order.Id, order.BusinessId, order.Status.ToString(), order.Payment.Status.ToString(), isNew);

    private static string Items(Order order) =>
        string.Join(", ", order.Items.Take(3).Select(i => $"{i.Quantity} × {i.NameSnapshot}"))
        + (order.Items.Count > 3 ? $" and {order.Items.Count - 3} more" : string.Empty);

    private static string Rupees(decimal amount) =>
        "₹" + amount.ToString(amount % 1 == 0 ? "#,##0" : "#,##0.00", CultureInfo.GetCultureInfo("en-IN"));
}
