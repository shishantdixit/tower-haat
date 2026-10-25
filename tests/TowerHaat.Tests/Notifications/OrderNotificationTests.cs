using TowerHaat.Application.Notifications;
using TowerHaat.Application.Orders;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Notifications;

/// <summary>Who hears what, and when (B-05 new order alerts, C-07 status pushes, reminders).</summary>
public sealed class OrderNotificationTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private async Task<(Kitchen K, Guid Customer)> SetUpAsync()
    {
        var kitchen = await Kitchen.OpenAsync(_h);
        var customer = await _h.ResidentAsync();
        _h.Notifications.Clear();
        return (kitchen, customer);
    }

    private UserNotification Single(Guid userId) => Assert.Single(_h.Notifications.Items, n => n.UserId == userId);

    [Fact]
    public async Task New_order_alerts_the_owner_with_items_amount_and_flat()
    {
        var (k, customer) = await SetUpAsync();

        var order = await k.OrderAsync(customer, (k.Thali, 2), (k.Paratha, 1));

        var alert = Assert.Single(_h.Notifications.Items);
        Assert.Equal(k.OwnerId, alert.UserId);
        Assert.Equal("New order #1 · ₹320", alert.Title);
        Assert.Equal("2 × Veg thali, 1 × Aloo paratha for A-102. Accept within 30 minutes.", alert.Body);
        Assert.Equal($"/owner/orders/{order.Id}", alert.Url);
        Assert.True(alert.Event!.IsNewOrder);
        Assert.DoesNotContain("+91", alert.Body);
    }

    [Fact]
    public async Task Repeated_checkout_does_not_alert_twice()
    {
        var (k, customer) = await SetUpAsync();
        var request = k.Request([(k.Thali, 1)]);

        await _h.As(Society.AceDivinoId).Orders.PlaceAsync(customer, request, _h.Ct);
        await _h.As(Society.AceDivinoId).Orders.PlaceAsync(customer, request, _h.Ct);

        Assert.Single(_h.Notifications.Items);
    }

    [Fact]
    public async Task Customer_hears_every_step_the_owner_takes()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);

        Assert.Equal(
            ["Asha's Kitchen accepted order #1", "Order #1 is being prepared", "Order #1 is on the way", "Order #1 delivered"],
            _h.Notifications.Items.Where(n => n.UserId == customer).Select(n => n.Title));
        Assert.All(_h.Notifications.Items.Where(n => n.UserId == customer), n => Assert.Equal($"/customer/orders/{order.Id}", n.Url));
    }

    [Fact]
    public async Task Rejection_reason_reaches_the_customer()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        await k.Owner.RejectAsync(k.OwnerId, order.Id, new("Thali finished for today"), _h.Ct);

        var alert = Single(customer);
        Assert.Equal(("Asha's Kitchen could not take order #1", "Thali finished for today"), (alert.Title, alert.Body));
    }

    [Fact]
    public async Task Customer_cancel_alerts_the_owner()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));
        _h.Notifications.Clear();

        await _h.As(Society.AceDivinoId).Orders.CancelAsync(customer, order.Id, _h.Ct);

        Assert.Equal("Order #1 cancelled", Single(k.OwnerId).Title);
    }

    [Fact]
    public async Task Owner_is_reminded_once_after_10_minutes()
    {
        var (k, customer) = await SetUpAsync();
        await k.OrderAsync(customer, (k.Thali, 1));
        _h.Notifications.Clear();
        var views = _h.As(Society.AceDivinoId).Views;

        _h.Time.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(0, await _h.As(Society.AceDivinoId).Views.RemindDueAsync(_h.Ct));

        _h.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await _h.As(Society.AceDivinoId).Views.RemindDueAsync(_h.Ct));
        Assert.Equal(0, await _h.As(Society.AceDivinoId).Views.RemindDueAsync(_h.Ct));

        var reminder = Single(k.OwnerId);
        Assert.Equal(("Order #1 is waiting", "Accept it within 19 minutes or it is cancelled automatically."), (reminder.Title, reminder.Body));
        Assert.NotNull(views);
    }

    [Fact]
    public async Task Accepted_orders_get_no_reminder()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);

        _h.Time.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(0, await _h.As(Society.AceDivinoId).Views.RemindDueAsync(_h.Ct));
    }

    [Fact]
    public async Task Expiry_tells_both_sides()
    {
        var (k, customer) = await SetUpAsync();
        await k.OrderAsync(customer, (k.Thali, 1));
        _h.Notifications.Clear();

        _h.Time.Advance(TimeSpan.FromMinutes(31));
        await _h.As(Society.AceDivinoId).Views.ExpireDueAsync(_h.Ct);

        Assert.Equal("Order #1 was not accepted", Single(customer).Title);
        Assert.Equal("Order #1 expired", Single(k.OwnerId).Title);
    }

    [Fact]
    public async Task UPI_claims_reach_the_owner_and_disputes_reach_the_customer()
    {
        var (k, customer) = await SetUpAsync();
        var order = await _h.As(Society.AceDivinoId).Orders.PlaceAsync(customer, k.Request([(k.Thali, 1)], PaymentMethod.Upi), _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        Assert.Contains("Pay ₹140 by UPI", Single(customer).Body);
        _h.Notifications.Clear();

        await _h.As(Society.AceDivinoId).Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(null), _h.Ct);
        await k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Dispute, "Not received yet"), _h.Ct);

        Assert.Equal("Resident 2 paid ₹140 by UPI", Single(k.OwnerId).Title);
        Assert.Equal(("Asha's Kitchen has not received your payment", "Not received yet"), (Single(customer).Title, Single(customer).Body));
    }

    public void Dispose() => _h.Dispose();
}
