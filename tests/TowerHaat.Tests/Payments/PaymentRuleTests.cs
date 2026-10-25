using TowerHaat.Application.Payments;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Tests.Payments;

/// <summary>C-08 and B-08 payment status rules.</summary>
public sealed class PaymentRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.CreateVersion7();

    private static Order NewOrder(PaymentMethod method)
    {
        var type = new BusinessType("Food", true, 1);
        var business = new Business(Owner, type, new Category(type.Id, "Home kitchen", 1), new BusinessProfile("K", null, "k@okaxis", null, "+919876543210"), Now);
        var thali = new CatalogItem(business, new CatalogItemDetails("Thali", null, 120, true, null, false, 0), Now);
        return new Order(business, Guid.CreateVersion7(), 1, new OrderCustomer("Ravi", "+919876500001", "A", "101"),
            [(thali, 1)], null, null, method, Guid.NewGuid(), Now);
    }

    private static Order Accepted(PaymentMethod method)
    {
        var order = NewOrder(method);
        order.Accept(Owner, Now);
        return order;
    }

    [Fact]
    public void UPI_can_be_paid_only_after_the_business_accepts()
    {
        var order = NewOrder(PaymentMethod.Upi);

        Assert.False(order.IsUpiPayable);
        Assert.Throws<InvalidOperationException>(() => order.MarkPaidByCustomer(null, Now));

        order.Accept(Owner, Now);
        Assert.True(order.IsUpiPayable);
    }

    [Fact]
    public void Customer_marks_paid_and_owner_confirms()
    {
        var order = Accepted(PaymentMethod.Upi);

        order.MarkPaidByCustomer("412345678901", Now);
        Assert.Equal((PaymentStatus.CustomerMarkedPaid, "412345678901"), (order.Payment.Status, order.Payment.Utr));
        Assert.False(order.IsUpiPayable);
        Assert.Throws<InvalidOperationException>(() => order.MarkPaidByCustomer(null, Now));

        order.ConfirmUpiReceived(Now);
        Assert.Equal(PaymentStatus.Confirmed, order.Payment.Status);
        Assert.Equal(Now, order.Payment.SettledAt);
    }

    [Fact]
    public void Owner_can_confirm_UPI_the_customer_forgot_to_mark()
    {
        var order = Accepted(PaymentMethod.Upi);

        order.ConfirmUpiReceived(Now);

        Assert.Equal(PaymentStatus.Confirmed, order.Payment.Status);
    }

    [Fact]
    public void Disputed_payment_can_be_marked_paid_again_or_confirmed()
    {
        var order = Accepted(PaymentMethod.Upi);
        order.MarkPaidByCustomer(null, Now);

        order.DisputePayment("No money in my account yet", Now);
        Assert.Equal((PaymentStatus.Disputed, "No money in my account yet"), (order.Payment.Status, order.Payment.Note));
        Assert.True(order.IsUpiPayable);

        order.MarkPaidByCustomer("412345678901", Now);
        Assert.Equal(PaymentStatus.CustomerMarkedPaid, order.Payment.Status);
        Assert.Null(order.Payment.Note);

        order.DisputePayment(null, Now);
        order.ConfirmUpiReceived(Now);
        Assert.Equal(PaymentStatus.Confirmed, order.Payment.Status);
    }

    [Fact]
    public void Only_a_payment_marked_paid_can_be_disputed()
    {
        Assert.Throws<InvalidOperationException>(() => Accepted(PaymentMethod.Upi).DisputePayment(null, Now));
    }

    [Fact]
    public void Placed_or_refused_orders_cannot_be_confirmed()
    {
        Assert.Throws<InvalidOperationException>(() => NewOrder(PaymentMethod.Upi).ConfirmUpiReceived(Now));

        var rejected = NewOrder(PaymentMethod.Upi);
        rejected.Reject(Owner, "Closed", Now);
        Assert.Throws<InvalidOperationException>(() => rejected.ConfirmUpiReceived(Now));
    }

    [Fact]
    public void Cash_is_collected_at_delivery_only()
    {
        var order = Accepted(PaymentMethod.Cod);
        Assert.Throws<InvalidOperationException>(() => order.MarkCashCollected(Now));

        order.Advance(Owner, Now);
        order.Advance(Owner, Now);
        order.MarkCashCollected(Now);

        Assert.Equal(PaymentStatus.Collected, order.Payment.Status);
        Assert.Throws<InvalidOperationException>(() => order.MarkCashCollected(Now));
    }

    [Fact]
    public void Methods_do_not_mix()
    {
        var cod = Accepted(PaymentMethod.Cod);
        var upi = Accepted(PaymentMethod.Upi);

        Assert.False(cod.IsUpiPayable);
        Assert.Throws<InvalidOperationException>(() => cod.MarkPaidByCustomer(null, Now));
        Assert.Throws<InvalidOperationException>(() => cod.ConfirmUpiReceived(Now));
        Assert.Throws<InvalidOperationException>(() => upi.MarkCashCollected(Now));
    }

    [Fact]
    public void Refund_needs_money_to_have_moved()
    {
        var order = Accepted(PaymentMethod.Upi);
        Assert.Throws<InvalidOperationException>(() => order.MarkRefunded(Now));

        order.MarkPaidByCustomer(null, Now);
        order.ConfirmUpiReceived(Now);
        order.CancelByOwner(Owner, "Gas ran out", Now);
        order.MarkRefunded(Now);

        Assert.Equal(PaymentStatus.Refunded, order.Payment.Status);
        Assert.Throws<InvalidOperationException>(() => order.ConfirmUpiReceived(Now));
    }

    [Fact]
    public void Cancelled_by_owner_order_is_not_payable()
    {
        var order = Accepted(PaymentMethod.Upi);
        order.CancelByOwner(Owner, "Gas ran out", Now);

        Assert.False(order.IsUpiPayable);
    }

    [Fact]
    public void UPI_link_carries_the_ID_name_amount_and_order()
    {
        var link = UpiLink.Build("asha.k@okaxis", "Asha's Kitchen & Co", 260m, "TowerHaat order #12");

        Assert.Equal(
            "upi://pay?pa=asha.k%40okaxis&pn=Asha%27s%20Kitchen%20%26%20Co&am=260.00&cu=INR&tn=TowerHaat%20order%20%2312",
            link);
    }
}
