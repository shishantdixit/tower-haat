using System.Net.Http.Json;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Orders;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Orders;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

public sealed class PaymentFlowTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task UPI_order_is_paid_and_confirmed_over_HTTP()
    {
        var owner = factory.CreateHttpsClient();
        await factory.SignUpAsync(owner, "+919844400001", "Asha", _ct);
        var business = await (await owner.PostAsJsonAsync("/api/my/business",
                new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "Asha's Kitchen", null, "asha@okaxis", null, null), _ct))
            .Content.ReadFromJsonAsync<OwnerBusinessResponse>(HttpSignIn.Json, _ct);
        (await owner.PutAsJsonAsync("/api/my/business/settings", new BusinessSettingsRequest("00:00", "00:00", null, 0, 0), _ct)).EnsureSuccessStatusCode();
        var item = await (await owner.PostAsJsonAsync("/api/my/business/items", new SaveItemRequest("Thali", null, 120, true, null, true, false, 0), _ct))
            .Content.ReadFromJsonAsync<OwnerItemResponse>(_ct);
        var admin = factory.CreateHttpsClient();
        await factory.SignInAsync(admin, TowerHaatFactory.AdminPhone, _ct);
        (await admin.PostAsJsonAsync($"/api/admin/businesses/{business!.Id}/approve", new BusinessDecisionRequest(null), _ct)).EnsureSuccessStatusCode();

        var customer = factory.CreateHttpsClient();
        await factory.SignUpAsync(customer, "+919844400002", "Ravi", _ct);
        var order = await (await customer.PostAsJsonAsync("/api/orders",
                new PlaceOrderRequest(business.Id, [new(item!.Id, 1)], null, null, null, PaymentMethod.Upi, Guid.NewGuid()), HttpSignIn.Json, _ct))
            .Content.ReadFromJsonAsync<OrderResponse>(HttpSignIn.Json, _ct);
        (await owner.PostAsync($"/api/my/business/orders/{order!.Id}/accept", null, _ct)).EnsureSuccessStatusCode();

        var accepted = await customer.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", HttpSignIn.Json, _ct);
        Assert.StartsWith("upi://pay?pa=asha%40okaxis", accepted!.Payment.Upi!.Link);

        (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/paid", new MarkPaidRequest("412345678901"), _ct)).EnsureSuccessStatusCode();
        var confirm = await owner.PostAsJsonAsync($"/api/my/business/orders/{order.Id}/payment", new PaymentActionRequest(PaymentAction.Confirm, null), HttpSignIn.Json, _ct);
        confirm.EnsureSuccessStatusCode();

        Assert.Contains("\"status\":\"Confirmed\"", await confirm.Content.ReadAsStringAsync(_ct));
    }
}
