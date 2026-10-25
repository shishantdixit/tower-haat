using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Notifications;
using TowerHaat.Application.Orders;
using TowerHaat.Domain.Orders;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

/// <summary>M5 end to end: a new order reaches the owner's open app over SignalR and their phone by web push.</summary>
public sealed class LiveAlertTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_order_reaches_the_owner_live_and_by_push()
    {
        var owner = factory.CreateHttpsClient();
        await factory.SignUpAsync(owner, "+919855500001", "Asha", _ct);
        var business = await (await owner.PostAsJsonAsync("/api/my/business",
                new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "Asha's Kitchen", null, null, null, null), _ct))
            .Content.ReadFromJsonAsync<OwnerBusinessResponse>(HttpSignIn.Json, _ct);
        (await owner.PutAsJsonAsync("/api/my/business/settings", new BusinessSettingsRequest("00:00", "00:00", null, 0, 0), _ct)).EnsureSuccessStatusCode();
        var item = await (await owner.PostAsJsonAsync("/api/my/business/items", new SaveItemRequest("Thali", null, 120, true, null, true, false, 0), _ct))
            .Content.ReadFromJsonAsync<OwnerItemResponse>(_ct);
        var admin = factory.CreateHttpsClient();
        await factory.SignInAsync(admin, TowerHaatFactory.AdminPhone, _ct);
        (await admin.PostAsJsonAsync($"/api/admin/businesses/{business!.Id}/approve", new BusinessDecisionRequest(null), _ct)).EnsureSuccessStatusCode();

        // The owner's phone subscribes to push, plus an old phone the push service no longer knows.
        (await owner.PostAsJsonAsync("/api/push/subscriptions", new SubscribeRequest("https://push.example.com/phone", "key", "auth"), _ct)).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/push/subscriptions", new SubscribeRequest("https://push.example.com/old-phone", "key", "auth"), _ct)).EnsureSuccessStatusCode();
        factory.Push.GoneEndpoints.Add("https://push.example.com/old-phone");

        // The owner's app is open and listening.
        var token = owner.DefaultRequestHeaders.Authorization!.Parameter!;
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/orders"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        var heard = new TaskCompletionSource<OrderChangedEvent>();
        hub.On<OrderChangedEvent>("orderChanged", e => heard.TrySetResult(e));
        await hub.StartAsync(_ct);

        var customer = factory.CreateHttpsClient();
        await factory.SignUpAsync(customer, "+919855500002", "Ravi", _ct);
        var order = await (await customer.PostAsJsonAsync("/api/orders",
                new PlaceOrderRequest(business.Id, [new(item!.Id, 1)], null, null, null, PaymentMethod.Cod, Guid.NewGuid()), HttpSignIn.Json, _ct))
            .Content.ReadFromJsonAsync<OrderResponse>(HttpSignIn.Json, _ct);

        var live = await heard.Task.WaitAsync(TimeSpan.FromSeconds(10), _ct);
        Assert.Equal((order!.Id, true, "Placed"), (live.OrderId, live.IsNewOrder, live.Status));

        await WaitUntil(() => factory.Push.Sent.Count(s => s.Target.Endpoint.StartsWith("https://push.example.com/")) >= 2);
        var push = factory.Push.Sent.First(s => s.Target.Endpoint == "https://push.example.com/phone");
        var notification = System.Text.Json.JsonDocument.Parse(push.Payload).RootElement.GetProperty("notification");
        Assert.Equal("New order #1 · ₹120", notification.GetProperty("title").GetString());
        Assert.Equal($"/owner/orders/{order.Id}", notification.GetProperty("data").GetProperty("url").GetString());
        Assert.Equal(
            "navigateLastFocusedOrOpen",
            notification.GetProperty("data").GetProperty("onActionClick").GetProperty("default").GetProperty("operation").GetString());

        // The dead subscription is cleaned up after the push service said it is gone.
        await WaitUntil(() =>
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.PushSubscriptions.Count() == 1;
        });
    }

    [Fact]
    public async Task Hub_refuses_connections_without_a_token()
    {
        var response = await factory.CreateHttpsClient().PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_key_is_hidden_when_push_is_not_configured()
    {
        var client = factory.CreateHttpsClient();
        await factory.SignInAsync(client, "+919855500003", _ct);

        // The test host has no VAPID keys.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/push/public-key", _ct)).StatusCode);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }
}
