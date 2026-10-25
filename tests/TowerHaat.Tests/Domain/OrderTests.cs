using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Tests.Domain;

/// <summary>Rule 5: the order status flow.</summary>
public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Customer = Guid.CreateVersion7();

    private static Order NewOrder()
    {
        var type = new BusinessType("Food", true, 1);
        var business = new Business(Owner, type, new Category(type.Id, "Home kitchen", 1), new BusinessProfile("K", null, null, null, "+919876543210"), Now);
        business.UpdateSettings(BusinessHours.Default, 0, 20);
        var thali = new CatalogItem(business, new CatalogItemDetails("Thali", null, 120, true, null, false, 0), Now);
        var roti = new CatalogItem(business, new CatalogItemDetails("Roti", null, 10.5m, true, null, false, 0), Now);
        return new Order(
            business, Customer, 1, new OrderCustomer("Ravi", "+919876500001", "A", "101"),
            [(thali, 2), (roti, 4)], null, null, PaymentMethod.Cod, Guid.NewGuid(), Now);
    }

    [Fact]
    public void New_order_is_placed_with_totals_from_the_snapshot_prices()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal((282m, 20m, 302m), (order.Subtotal, order.DeliveryCharge, order.Total));
        Assert.Equal(["Thali", "Roti"], order.Items.Select(i => i.NameSnapshot));
        Assert.Single(order.History);
    }

    [Fact]
    public void Happy_path_goes_through_every_step_in_order()
    {
        var order = NewOrder();

        order.Accept(Owner, Now.AddMinutes(1));
        order.Advance(Owner, Now.AddMinutes(2));
        order.Advance(Owner, Now.AddMinutes(30));
        order.Advance(Owner, Now.AddMinutes(40));

        Assert.Equal(
            [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.OutForDelivery, OrderStatus.Delivered],
            order.History.Select(h => h.Status));
        Assert.Throws<InvalidOperationException>(() => order.Advance(Owner, Now.AddMinutes(41)));
    }

    [Fact]
    public void Placed_order_must_be_accepted_before_it_moves_on()
    {
        Assert.Throws<InvalidOperationException>(() => NewOrder().Advance(Owner, Now));
    }

    [Fact]
    public void Rejecting_needs_a_reason_and_only_works_while_placed()
    {
        var order = NewOrder();
        Assert.Throws<ArgumentException>(() => order.Reject(Owner, " ", Now));

        order.Reject(Owner, "Out of paneer today", Now);
        Assert.Equal((OrderStatus.Rejected, "Out of paneer today"), (order.Status, order.History[^1].Reason));

        var accepted = NewOrder();
        accepted.Accept(Owner, Now);
        Assert.Throws<InvalidOperationException>(() => accepted.Reject(Owner, "Too late", Now));
    }

    [Fact]
    public void Customer_can_cancel_only_while_placed()
    {
        var placed = NewOrder();
        placed.CancelByCustomer(Customer, Now);
        Assert.Equal(OrderStatus.CancelledByCustomer, placed.Status);

        var accepted = NewOrder();
        accepted.Accept(Owner, Now);
        Assert.Throws<InvalidOperationException>(() => accepted.CancelByCustomer(Customer, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Owner_can_cancel_after_accepting_with_a_reason(int steps)
    {
        var order = NewOrder();
        order.Accept(Owner, Now);
        for (var i = 1; i < steps; i++)
        {
            order.Advance(Owner, Now);
        }

        Assert.Throws<ArgumentException>(() => order.CancelByOwner(Owner, "", Now));
        order.CancelByOwner(Owner, "Gas cylinder ran out", Now);

        Assert.Equal(OrderStatus.CancelledByOwner, order.Status);
    }

    [Fact]
    public void Owner_cannot_cancel_a_placed_or_delivered_order()
    {
        Assert.Throws<InvalidOperationException>(() => NewOrder().CancelByOwner(Owner, "No", Now));

        var delivered = NewOrder();
        delivered.Accept(Owner, Now);
        delivered.Advance(Owner, Now);
        delivered.Advance(Owner, Now);
        delivered.Advance(Owner, Now);
        Assert.Throws<InvalidOperationException>(() => delivered.CancelByOwner(Owner, "No", Now));
    }

    [Fact]
    public void Unaccepted_order_expires_after_30_minutes_at_its_due_time()
    {
        var order = NewOrder();

        Assert.False(order.IsDueToExpire(Now.AddMinutes(29)));
        Assert.Throws<InvalidOperationException>(() => order.Expire(Now.AddMinutes(29)));

        order.Expire(Now.AddHours(3));

        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(Now.AddMinutes(30), order.History[^1].At);
        Assert.Null(order.History[^1].ChangedByUserId);
    }

    [Fact]
    public void Accepted_order_never_expires()
    {
        var order = NewOrder();
        order.Accept(Owner, Now.AddMinutes(5));

        Assert.False(order.IsDueToExpire(Now.AddHours(5)));
    }

    [Fact]
    public void Closed_orders_stay_closed()
    {
        var order = NewOrder();
        order.CancelByCustomer(Customer, Now);

        Assert.Throws<InvalidOperationException>(() => order.Accept(Owner, Now));
        Assert.True(order.Status.IsClosed());
    }
}
