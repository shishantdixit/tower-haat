using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Orders;

/// <summary>Order rules at checkout (C-05, C-06): every rule that loses money or leaks data is tested here.</summary>
public sealed class OrderPlacementTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private async Task<(Kitchen Kitchen, Guid Customer)> SetUpAsync()
    {
        var kitchen = await Kitchen.OpenAsync(_h);
        return (kitchen, await _h.ResidentAsync());
    }

    private Task<Application.Orders.OrderResponse> Place(Kitchen k, Guid customer, Application.Orders.PlaceOrderRequest request) =>
        _h.As(Society.AceDivinoId).Orders.PlaceAsync(customer, request, _h.Ct);

    private async Task<AppException> Refused(Kitchen k, Guid customer, Application.Orders.PlaceOrderRequest request) =>
        await Assert.ThrowsAsync<AppException>(() => Place(k, customer, request));

    [Fact]
    public async Task Order_is_placed_with_snapshots_totals_and_a_number()
    {
        var (k, customer) = await SetUpAsync();

        var order = await Place(k, customer, k.Request([(k.Thali, 1), (k.Paratha, 2)], note: "Less spicy"));

        Assert.Equal((OrderStatus.Placed, 1), (order.Status, order.OrderNo));
        Assert.Equal((240m, 20m, 260m), (order.Subtotal, order.DeliveryCharge, order.Total));
        Assert.Equal(("A", "Resident 2"), (order.Tower, order.CustomerName));
        Assert.Equal("Less spicy", order.Note);
        Assert.Null(order.PreferredTime);
        Assert.Equal(_h.Time.GetUtcNow().UtcDateTime.AddMinutes(30), order.ExpiresAt);

        var second = await k.OrderAsync(customer, (k.Thali, 1));
        Assert.Equal(2, second.OrderNo);
    }

    [Fact]
    public async Task Repeating_the_same_checkout_returns_the_same_order()
    {
        var (k, customer) = await SetUpAsync();
        var key = Guid.NewGuid();

        var first = await Place(k, customer, k.Request([(k.Thali, 1)], key: key));
        var again = await Place(k, customer, k.Request([(k.Thali, 1)], key: key));

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await _h.As(Society.AceDivinoId).Db.Orders.CountAsync(_h.Ct));
    }

    [Fact]
    public async Task Later_catalog_edits_do_not_change_past_orders()
    {
        var (k, customer) = await SetUpAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));

        await _h.As(Society.AceDivinoId).Catalog.UpdateAsync(
            k.OwnerId, k.Thali, new SaveItemRequest("Deluxe thali", null, 200, true, 20, true, false, 1), _h.Ct);
        await _h.As(Society.AceDivinoId).Catalog.DeleteAsync(k.OwnerId, k.Paratha, _h.Ct);

        var reread = await _h.As(Society.AceDivinoId).Orders.GetMineAsync(customer, order.Id, _h.Ct);
        Assert.Equal(("Veg thali", 120m, 140m), (reread.Items[0].Name, reread.Items[0].Price, reread.Total));
    }

    [Fact]
    public async Task Closed_switch_blocks_orders()
    {
        var (k, customer) = await SetUpAsync();
        await _h.As(Society.AceDivinoId).Owner.SetOpenAsync(k.OwnerId, false, _h.Ct);

        var error = await Refused(k, customer, k.Request([(k.Thali, 1)]));

        Assert.Contains("closed", error.Message);
    }

    [Fact]
    public async Task Outside_opening_hours_blocks_orders()
    {
        var (k, customer) = await SetUpAsync();
        _h.Time.Advance(TimeSpan.FromHours(10)); // 21:30 in India

        Assert.Contains("closed", (await Refused(k, customer, k.Request([(k.Thali, 1)]))).Message);
    }

    [Fact]
    public async Task Past_the_last_order_time_blocks_orders()
    {
        var (k, customer) = await SetUpAsync();
        _h.Time.Advance(TimeSpan.FromHours(9)); // 20:30: open until 21:00, orders until 20:00

        Assert.Contains("stopped taking orders", (await Refused(k, customer, k.Request([(k.Thali, 1)]))).Message);
    }

    [Fact]
    public async Task Below_the_minimum_order_is_refused()
    {
        var (k, customer) = await SetUpAsync();

        var error = await Refused(k, customer, k.Request([(k.Paratha, 1)]));

        Assert.Contains("₹40 more", error.Message);
    }

    [Fact]
    public async Task Sold_out_item_blocks_checkout()
    {
        var (k, customer) = await SetUpAsync();
        await _h.As(Society.AceDivinoId).Catalog.SetAvailabilityAsync(k.OwnerId, k.Thali, false, _h.Ct);

        var error = await Refused(k, customer, k.Request([(k.Thali, 1)]));

        Assert.Contains("sold out", error.Message);
    }

    [Fact]
    public async Task Daily_limit_counts_todays_orders_and_gets_stock_back_from_refused_ones()
    {
        var (k, customer) = await SetUpAsync();
        var first = await k.OrderAsync(customer, (k.Thali, 15));

        var error = await Refused(k, customer, k.Request([(k.Thali, 6)]));
        Assert.Contains("Only 5", error.Message);
        await k.OrderAsync(customer, (k.Thali, 5));
        Assert.Contains("sold out", (await Refused(k, customer, k.Request([(k.Thali, 1)]))).Message);

        await k.Owner.RejectAsync(k.OwnerId, first.Id, new("Too many today"), _h.Ct);
        await k.OrderAsync(customer, (k.Thali, 15));
    }

    [Fact]
    public async Task Daily_limit_resets_at_midnight_in_india()
    {
        var (k, customer) = await SetUpAsync();
        await k.OrderAsync(customer, (k.Thali, 20));

        _h.Time.Advance(TimeSpan.FromHours(21)); // 08:30 the next morning
        await k.OrderAsync(customer, (k.Thali, 20));
    }

    [Fact]
    public async Task Sold_out_and_remaining_show_on_the_business_page()
    {
        var (k, customer) = await SetUpAsync();
        await k.OrderAsync(customer, (k.Thali, 18));

        var page = await _h.As(Society.AceDivinoId).Directory.GetAsync(customer, k.BusinessId, _h.Ct);

        Assert.Equal(2, page.Items.Single(i => i.Id == k.Thali).RemainingToday);
    }

    [Fact]
    public async Task Hidden_and_foreign_items_are_refused()
    {
        var (k, customer) = await SetUpAsync();
        var other = await Kitchen.OpenAsync(_h, "Ravi's Bakes");
        await _h.As(Society.AceDivinoId).Catalog.UpdateAsync(
            k.OwnerId, k.Paratha, new SaveItemRequest("Aloo paratha", null, 60, true, null, true, IsHidden: true, 2), _h.Ct);

        await Refused(k, customer, k.Request([(k.Thali, 1), (k.Paratha, 1)]));
        await Refused(k, customer, k.Request([(k.Thali, 1), (other.Thali, 1)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Quantities_are_checked(int quantity)
    {
        var (k, customer) = await SetUpAsync();

        await Refused(k, customer, k.Request([(k.Paratha, quantity), (k.Thali, 1)]));
    }

    [Fact]
    public async Task Empty_cart_is_refused()
    {
        var (k, customer) = await SetUpAsync();

        await Refused(k, customer, k.Request([]));
    }

    [Fact]
    public async Task UPI_needs_the_business_to_have_a_UPI_ID()
    {
        var (k, customer) = await SetUpAsync();
        await Place(k, customer, k.Request([(k.Thali, 1)], PaymentMethod.Upi));

        await _h.As(Society.AceDivinoId).Owner.UpdateProfileAsync(
            k.OwnerId, new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "Asha's Kitchen", null, UpiId: null, null, null), _h.Ct);

        Assert.Contains("cash", (await Refused(k, customer, k.Request([(k.Thali, 1)], PaymentMethod.Upi))).Message);
    }

    [Fact]
    public async Task Preferred_time_later_today_or_tomorrow_within_hours()
    {
        var (k, customer) = await SetUpAsync();

        var today = await Place(k, customer, k.Request([(k.Thali, 1)], day: "today", at: "13:00"));
        var tomorrow = await Place(k, customer, k.Request([(k.Thali, 1)], day: "tomorrow", at: "09:30"));

        Assert.Equal(new DateTime(2026, 10, 25, 7, 30, 0, DateTimeKind.Utc), today.PreferredTime);
        Assert.Equal(new DateTime(2026, 10, 26, 4, 0, 0, DateTimeKind.Utc), tomorrow.PreferredTime);
    }

    [Theory]
    [InlineData("today", "11:40")] // less than 15 minutes ahead
    [InlineData("today", "10:00")] // in the past
    [InlineData("tomorrow", "22:00")] // after closing
    [InlineData("tomorrow", "")]
    [InlineData("monday", "12:00")]
    public async Task Invalid_preferred_times_are_refused(string day, string at)
    {
        var (k, customer) = await SetUpAsync();

        await Refused(k, customer, k.Request([(k.Thali, 1)], day: day, at: at));
    }

    [Fact]
    public async Task Owner_cannot_order_from_their_own_business()
    {
        var (k, _) = await SetUpAsync();

        await Refused(k, k.OwnerId, k.Request([(k.Thali, 1)]));
    }

    [Fact]
    public async Task Pending_or_deactivated_business_takes_no_orders()
    {
        var (k, customer) = await SetUpAsync();
        await _h.Admin().Admin.DeactivateAsync(k.BusinessId, new BusinessDecisionRequest("Paused"), _h.Ct);

        await Refused(k, customer, k.Request([(k.Thali, 1)]));
    }

    [Fact]
    public async Task Resident_of_another_society_cannot_order()
    {
        var (k, _) = await SetUpAsync();
        var outsider = await _h.ResidentAsync(_h.OtherSociety.Id);

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(_h.OtherSociety.Id).Orders.PlaceAsync(outsider, k.Request([(k.Thali, 1)]), _h.Ct));

        Assert.Contains("not taking orders", error.Message);
    }

    [Fact]
    public async Task Listing_only_business_takes_no_orders()
    {
        var (owner, business) = await _h.BusinessAsync("Riya's Music", typeId: BusinessHarness.Tuition, categoryId: BusinessHarness.Music);
        var customer = await _h.ResidentAsync();

        await Assert.ThrowsAsync<AppException>(() => _h.As(Society.AceDivinoId).Orders.PlaceAsync(
            customer,
            new(business, [new(Guid.NewGuid(), 1)], null, null, null, PaymentMethod.Cod, Guid.NewGuid()),
            _h.Ct));
        Assert.NotEqual(Guid.Empty, owner);
    }

    public void Dispose() => _h.Dispose();
}
