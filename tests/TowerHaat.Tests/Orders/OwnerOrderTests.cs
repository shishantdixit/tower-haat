using TowerHaat.Application.Common;
using TowerHaat.Application.Orders;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Orders;

/// <summary>B-05 dashboard, B-06 status actions, B-07 prep summary, C-07/C-09 for the customer, and expiry.</summary>
public sealed class OwnerOrderTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private async Task<(Kitchen Kitchen, Guid Customer)> SetUpAsync()
    {
        var kitchen = await Kitchen.OpenAsync(_h);
        return (kitchen, await _h.ResidentAsync());
    }

    private OrderService Orders => _h.As(Society.AceDivinoId).Orders;

    [Fact]
    public async Task Owner_moves_an_order_through_to_delivered_and_the_customer_sees_the_timeline()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        _h.Time.Advance(TimeSpan.FromMinutes(5));
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);

        var seen = await Orders.GetMineAsync(customer, order.Id, _h.Ct);
        Assert.Equal(OrderStatus.Delivered, seen.Status);
        Assert.Equal(
            [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.OutForDelivery, OrderStatus.Delivered],
            seen.History.Select(h => h.Status));
        Assert.Null(seen.ExpiresAt);
    }

    [Fact]
    public async Task Reject_and_owner_cancel_need_a_reason_the_customer_sees()
    {
        var (k, customer) = await SetUpAsync();
        var rejected = await k.OrderAsync(customer, (k.Thali, 1));
        var cancelled = await k.OrderAsync(customer, (k.Thali, 1));

        await Assert.ThrowsAsync<AppException>(() => k.Owner.RejectAsync(k.OwnerId, rejected.Id, new(" "), _h.Ct));
        await k.Owner.RejectAsync(k.OwnerId, rejected.Id, new("Kitchen closed early"), _h.Ct);

        await k.Owner.AcceptAsync(k.OwnerId, cancelled.Id, _h.Ct);
        await Assert.ThrowsAsync<AppException>(() => k.Owner.CancelAsync(k.OwnerId, cancelled.Id, new(null), _h.Ct));
        await k.Owner.CancelAsync(k.OwnerId, cancelled.Id, new("Gas ran out"), _h.Ct);

        Assert.Equal("Kitchen closed early", (await Orders.GetMineAsync(customer, rejected.Id, _h.Ct)).History[^1].Reason);
        Assert.Equal(OrderStatus.CancelledByOwner, (await Orders.GetMineAsync(customer, cancelled.Id, _h.Ct)).Status);
    }

    [Fact]
    public async Task Customer_cancels_only_before_acceptance()
    {
        var (k, customer) = await SetUpAsync();
        var early = await k.OrderAsync(customer, (k.Thali, 1));
        var late = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.AcceptAsync(k.OwnerId, late.Id, _h.Ct);

        var cancelled = await Orders.CancelAsync(customer, early.Id, _h.Ct);
        var error = await Assert.ThrowsAsync<AppException>(() => Orders.CancelAsync(customer, late.Id, _h.Ct));

        Assert.Equal(OrderStatus.CancelledByCustomer, cancelled.Status);
        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Invalid_steps_are_conflicts()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        var error = await Assert.ThrowsAsync<AppException>(() => k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Another_business_cannot_see_or_change_the_order()
    {
        var (k, customer) = await SetUpAsync();
        var rival = await Kitchen.OpenAsync(_h, "Ravi's Bakes");
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        await Assert.ThrowsAsync<AppException>(() => rival.Owner.GetAsync(rival.OwnerId, order.Id, _h.Ct));
        await Assert.ThrowsAsync<AppException>(() => rival.Owner.AcceptAsync(rival.OwnerId, order.Id, _h.Ct));
        Assert.Empty((await rival.Owner.ListAsync(rival.OwnerId, OwnerOrderTab.New, _h.Ct)).Orders);
    }

    [Fact]
    public async Task Customers_see_only_their_own_orders()
    {
        var (k, asha) = await SetUpAsync();
        var ravi = await _h.ResidentAsync();
        var order = await k.OrderAsync(asha, (k.Thali, 1));
        await k.OrderAsync(ravi, (k.Paratha, 2));

        Assert.Equal([order.Id], (await Orders.ListMineAsync(asha, _h.Ct)).Select(o => o.Id));
        await Assert.ThrowsAsync<AppException>(() => Orders.GetMineAsync(ravi, order.Id, _h.Ct));
    }

    [Fact]
    public async Task Owner_sees_the_customers_phone_and_flat_on_their_orders()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        var seen = await k.Owner.GetAsync(k.OwnerId, order.Id, _h.Ct);

        Assert.StartsWith("+9198765", seen.CustomerPhone);
        Assert.Equal("A", seen.Tower);
    }

    [Fact]
    public async Task Dashboard_groups_orders_by_tab_with_counts()
    {
        var (k, customer) = await SetUpAsync();
        var placed = await k.OrderAsync(customer, (k.Thali, 1));
        var preparing = await k.OrderAsync(customer, (k.Thali, 1));
        var delivering = await k.OrderAsync(customer, (k.Thali, 1));
        var done = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.AcceptAsync(k.OwnerId, preparing.Id, _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, delivering.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, delivering.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, delivering.Id, _h.Ct);
        await k.Owner.RejectAsync(k.OwnerId, done.Id, new("No"), _h.Ct);

        var dashboard = await k.Owner.ListAsync(k.OwnerId, OwnerOrderTab.New, _h.Ct);

        Assert.Equal(
            new Dictionary<OwnerOrderTab, int> { [OwnerOrderTab.New] = 1, [OwnerOrderTab.Preparing] = 1, [OwnerOrderTab.OutForDelivery] = 1, [OwnerOrderTab.Done] = 1 },
            dashboard.Counts);
        Assert.Equal([placed.Id], dashboard.Orders.Select(o => o.Id));
        Assert.Equal("1 × Veg thali", dashboard.Orders[0].ItemsText);
        Assert.Equal([delivering.Id], (await k.Owner.ListAsync(k.OwnerId, OwnerOrderTab.OutForDelivery, _h.Ct)).Orders.Select(o => o.Id));
    }

    [Fact]
    public async Task Unaccepted_order_expires_after_30_minutes_when_read()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 20));

        _h.Time.Advance(TimeSpan.FromMinutes(31));
        var seen = await Orders.GetMineAsync(customer, order.Id, _h.Ct);

        Assert.Equal(OrderStatus.Expired, seen.Status);
        await Assert.ThrowsAsync<AppException>(() => k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct));

        // Its stock is back on sale.
        await k.OrderAsync(customer, (k.Thali, 20));
    }

    [Fact]
    public async Task Owner_accepting_just_in_time_wins_over_expiry()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        _h.Time.Advance(TimeSpan.FromMinutes(29));
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        _h.Time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(OrderStatus.Accepted, (await Orders.GetMineAsync(customer, order.Id, _h.Ct)).Status);
    }

    [Fact]
    public async Task Prep_summary_totals_todays_items()
    {
        var (k, customer) = await SetUpAsync();
        var a = await k.OrderAsync(customer, (k.Thali, 2), (k.Paratha, 3));
        var b = await k.OrderAsync(customer, (k.Thali, 1));
        await k.OrderAsync(customer, (k.Paratha, 2));
        var tomorrow = await _h.As(Society.AceDivinoId).Orders.PlaceAsync(
            customer, k.Request([(k.Thali, 5)], day: "tomorrow", at: "12:00"), _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, a.Id, _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, b.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, b.Id, _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, tomorrow.Id, _h.Ct);

        var summary = await k.Owner.PrepSummaryAsync(k.OwnerId, _h.Ct);

        Assert.Equal((2, 1), (summary.OrdersToPrepare, summary.OrdersAwaitingAcceptance));
        Assert.Equal(
            [new PrepLine("Aloo paratha", true, 3, 2), new PrepLine("Veg thali", true, 3, 0)],
            summary.Items);
    }

    public void Dispose() => _h.Dispose();
}
