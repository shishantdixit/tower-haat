using TowerHaat.Application.Businesses;
using TowerHaat.Application.Common;
using TowerHaat.Application.Orders;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Payments;

/// <summary>C-08 and B-08 through the services, as the customer and the owner use them.</summary>
public sealed class PaymentServiceTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private OrderService Orders => _h.As(Society.AceDivinoId).Orders;

    private async Task<(Kitchen Kitchen, Guid Customer, OrderResponse Order)> UpiOrderAsync()
    {
        var kitchen = await Kitchen.OpenAsync(_h);
        var customer = await _h.ResidentAsync();
        var order = await Orders.PlaceAsync(customer, kitchen.Request([(kitchen.Thali, 2)], PaymentMethod.Upi), _h.Ct);
        return (kitchen, customer, order);
    }

    [Fact]
    public async Task UPI_details_appear_only_after_acceptance_and_until_paid()
    {
        var (k, customer, order) = await UpiOrderAsync();
        Assert.Null(order.Payment.Upi);

        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        var accepted = await Orders.GetMineAsync(customer, order.Id, _h.Ct);
        var upi = accepted.Payment.Upi!;
        Assert.Equal(("asha@okaxis", "Asha's Kitchen", 260m), (upi.UpiId, upi.PayeeName, upi.Amount));
        Assert.Contains("am=260.00", upi.Link);
        Assert.Contains("order%20%231", upi.Link);

        var paid = await Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest("4123 4567 8901"), _h.Ct);
        Assert.Equal((PaymentStatus.CustomerMarkedPaid, "412345678901"), (paid.Payment.Status, paid.Payment.Utr));
        Assert.Null(paid.Payment.Upi);
    }

    [Fact]
    public async Task Owner_sees_the_claim_and_confirms_it()
    {
        var (k, customer, order) = await UpiOrderAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        await Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(null), _h.Ct);

        var list = await k.Owner.ListAsync(k.OwnerId, OwnerOrderTab.Preparing, _h.Ct);
        Assert.Equal(PaymentStatus.CustomerMarkedPaid, list.Orders.Single().PaymentStatus);

        var confirmed = await k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Confirm, null), _h.Ct);
        Assert.Equal(PaymentStatus.Confirmed, confirmed.Payment.Status);
    }

    [Fact]
    public async Task Paying_before_acceptance_is_refused()
    {
        var (_, customer, order) = await UpiOrderAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(null), _h.Ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
        Assert.Contains("once the business accepts", error.Message);
    }

    [Fact]
    public async Task Dispute_note_reaches_the_customer()
    {
        var (k, customer, order) = await UpiOrderAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        await Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(null), _h.Ct);

        await k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Dispute, "Not received, please check"), _h.Ct);
        var seen = await Orders.GetMineAsync(customer, order.Id, _h.Ct);

        Assert.Equal((PaymentStatus.Disputed, "Not received, please check"), (seen.Payment.Status, seen.Payment.Note));
        Assert.NotNull(seen.Payment.Upi);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("4123-4567-8901")]
    public async Task Invalid_UTR_is_refused(string utr)
    {
        var (k, customer, order) = await UpiOrderAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);

        await Assert.ThrowsAsync<AppException>(
            () => Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(utr), _h.Ct));
    }

    [Fact]
    public async Task Cash_order_is_collected_after_it_goes_out()
    {
        var k = await Kitchen.OpenAsync(_h);
        var customer = await _h.ResidentAsync();
        var order = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);

        await Assert.ThrowsAsync<AppException>(
            () => k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Collect, null), _h.Ct));
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        await k.Owner.AdvanceAsync(k.OwnerId, order.Id, _h.Ct);
        var collected = await k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Collect, null), _h.Ct);

        Assert.Equal(PaymentStatus.Collected, collected.Payment.Status);
        Assert.Null(collected.Payment.Upi);
    }

    [Fact]
    public async Task Refund_after_cancelling_a_paid_order()
    {
        var (k, customer, order) = await UpiOrderAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        await Orders.MarkPaidAsync(customer, order.Id, new MarkPaidRequest(null), _h.Ct);
        await k.Owner.CancelAsync(k.OwnerId, order.Id, new("Gas ran out"), _h.Ct);

        var refunded = await k.Owner.UpdatePaymentAsync(k.OwnerId, order.Id, new(PaymentAction.Refund, null), _h.Ct);

        Assert.Equal(PaymentStatus.Refunded, refunded.Payment.Status);
        Assert.Equal(PaymentStatus.Refunded, (await Orders.GetMineAsync(customer, order.Id, _h.Ct)).Payment.Status);
    }

    [Fact]
    public async Task Others_cannot_touch_the_payment()
    {
        var (k, _, order) = await UpiOrderAsync();
        var rival = await Kitchen.OpenAsync(_h, "Ravi's Bakes");
        var stranger = await _h.ResidentAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);

        await Assert.ThrowsAsync<AppException>(
            () => rival.Owner.UpdatePaymentAsync(rival.OwnerId, order.Id, new(PaymentAction.Confirm, null), _h.Ct));
        await Assert.ThrowsAsync<AppException>(
            () => Orders.MarkPaidAsync(stranger, order.Id, new MarkPaidRequest(null), _h.Ct));
    }

    [Fact]
    public async Task UPI_details_follow_the_businesss_current_UPI_ID()
    {
        var (k, customer, order) = await UpiOrderAsync();
        await k.Owner.AcceptAsync(k.OwnerId, order.Id, _h.Ct);
        await _h.As(Society.AceDivinoId).Owner.UpdateProfileAsync(
            k.OwnerId, new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "Asha's Kitchen", null, "asha.new@okicici", null, null), _h.Ct);

        var seen = await Orders.GetMineAsync(customer, order.Id, _h.Ct);

        Assert.Equal("asha.new@okicici", seen.Payment.Upi!.UpiId);
    }

    public void Dispose() => _h.Dispose();
}
