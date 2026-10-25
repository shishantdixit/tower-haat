using System.Net;
using System.Net.Http.Json;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Orders;
using TowerHaat.Domain.Orders;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

/// <summary>Checkout, owner actions and the expiry job over HTTP.</summary>
public sealed class OrderFlowTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<(HttpClient Owner, Guid BusinessId, Guid ItemId)> KitchenAsync(string ownerPhone, string adminPhone)
    {
        var owner = factory.CreateHttpsClient();
        await factory.SignUpAsync(owner, ownerPhone, "Asha", _ct);
        var business = await (await owner.PostAsJsonAsync("/api/my/business",
                new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "Asha's Kitchen", null, "asha@okaxis", null, null), _ct))
            .Content.ReadFromJsonAsync<OwnerBusinessResponse>(HttpSignIn.Json, _ct);
        (await owner.PutAsJsonAsync("/api/my/business/settings", new BusinessSettingsRequest("00:00", "00:00", null, 0, 0), _ct))
            .EnsureSuccessStatusCode();
        var item = await (await owner.PostAsJsonAsync("/api/my/business/items",
                new SaveItemRequest("Veg thali", null, 120, true, null, true, false, 0), _ct))
            .Content.ReadFromJsonAsync<OwnerItemResponse>(_ct);

        var admin = factory.CreateHttpsClient();
        await factory.SignInAsync(admin, adminPhone, _ct);
        (await admin.PostAsJsonAsync($"/api/admin/businesses/{business!.Id}/approve", new BusinessDecisionRequest(null), _ct))
            .EnsureSuccessStatusCode();

        return (owner, business.Id, item!.Id);
    }

    [Fact]
    public async Task Customer_orders_twice_with_one_key_and_the_owner_accepts()
    {
        var (owner, businessId, itemId) = await KitchenAsync("+919822200001", TowerHaatFactory.AdminPhone);
        var customer = factory.CreateHttpsClient();
        await factory.SignUpAsync(customer, "+919822200002", "Ravi", _ct);
        var request = new PlaceOrderRequest(businessId, [new(itemId, 2)], "Ring the bell", null, null, PaymentMethod.Upi, Guid.NewGuid());

        var first = await customer.PostAsJsonAsync("/api/orders", request, HttpSignIn.Json, _ct);
        var second = await customer.PostAsJsonAsync("/api/orders", request, HttpSignIn.Json, _ct);
        first.EnsureSuccessStatusCode();
        var order = (await first.Content.ReadFromJsonAsync<OrderResponse>(HttpSignIn.Json, _ct))!;
        Assert.Equal(order.Id, (await second.Content.ReadFromJsonAsync<OrderResponse>(HttpSignIn.Json, _ct))!.Id);

        var dashboard = await owner.GetFromJsonAsync<OwnerOrdersResponse>("/api/my/business/orders?tab=New", HttpSignIn.Json, _ct);
        Assert.Equal([order.Id], dashboard!.Orders.Select(o => o.Id));

        var accepted = await owner.PostAsync($"/api/my/business/orders/{order.Id}/accept", null, _ct);
        accepted.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"Accepted\"", await accepted.Content.ReadAsStringAsync(_ct));

        var cancel = await customer.PostAsync($"/api/orders/{order.Id}/cancel", null, _ct);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);

        var mine = await customer.GetFromJsonAsync<List<OrderSummaryResponse>>("/api/orders", HttpSignIn.Json, _ct);
        Assert.Equal(OrderStatus.Accepted, mine!.Single().Status);
    }

    [Fact]
    public async Task Customer_cannot_open_the_owners_view_of_an_order()
    {
        var (_, businessId, itemId) = await KitchenAsync("+919822200003", TowerHaatFactory.SecondAdminPhone);
        var customer = factory.CreateHttpsClient();
        await factory.SignUpAsync(customer, "+919822200004", "Meena", _ct);
        var order = await (await customer.PostAsJsonAsync("/api/orders",
                new PlaceOrderRequest(businessId, [new(itemId, 1)], null, null, null, PaymentMethod.Cod, Guid.NewGuid()), HttpSignIn.Json, _ct))
            .Content.ReadFromJsonAsync<OrderResponse>(HttpSignIn.Json, _ct);

        var response = await customer.GetAsync($"/api/my/business/orders/{order!.Id}", _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Expiry_job_needs_the_key()
    {
        var client = factory.CreateHttpsClient();

        var without = await client.PostAsync("/api/jobs/expire-orders", null, _ct);
        var wrong = new HttpRequestMessage(HttpMethod.Post, "/api/jobs/expire-orders") { Headers = { { "X-Job-Key", "wrong" } } };
        var right = new HttpRequestMessage(HttpMethod.Post, "/api/jobs/expire-orders") { Headers = { { "X-Job-Key", TowerHaatFactory.JobKey } } };

        Assert.Equal(HttpStatusCode.NotFound, without.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(wrong, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(right, _ct)).StatusCode);

        // Schedulers that can only open a URL pass the key in the query string.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/jobs/expire-orders?key={TowerHaatFactory.JobKey}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/jobs/expire-orders?key=wrong", _ct)).StatusCode);
    }
}
