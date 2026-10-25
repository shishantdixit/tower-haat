using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Common;
using TowerHaat.Application.Notifications;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Notifications;

public sealed class PushSubscriptionTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private static SubscribeRequest Device(int n) => new($"https://fcm.googleapis.com/fcm/send/device-{n}", $"p256dh-{n}", $"auth-{n}");

    private PushSubscriptionService Push => _h.As(Society.AceDivinoId).Push;

    [Fact]
    public async Task Subscribing_twice_from_one_browser_keeps_one_row()
    {
        var user = await _h.ResidentAsync();

        await Push.SubscribeAsync(user, Device(1), _h.Ct);
        await Push.SubscribeAsync(user, Device(1) with { P256dh = "new-key" }, _h.Ct);

        var saved = await _h.As(Society.AceDivinoId).Db.PushSubscriptions.SingleAsync(_h.Ct);
        Assert.Equal("new-key", saved.P256dh);
    }

    [Fact]
    public async Task Another_user_signing_in_on_the_browser_takes_it_over()
    {
        var asha = await _h.ResidentAsync();
        var ravi = await _h.ResidentAsync();

        await Push.SubscribeAsync(asha, Device(1), _h.Ct);
        await Push.SubscribeAsync(ravi, Device(1), _h.Ct);

        Assert.Equal(ravi, (await _h.As(Society.AceDivinoId).Db.PushSubscriptions.SingleAsync(_h.Ct)).UserId);
    }

    [Fact]
    public async Task Only_the_newest_ten_devices_are_kept()
    {
        var user = await _h.ResidentAsync();
        for (var i = 1; i <= 12; i++)
        {
            await Push.SubscribeAsync(user, Device(i), _h.Ct);
            _h.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var endpoints = await _h.As(Society.AceDivinoId).Db.PushSubscriptions.Select(s => s.Endpoint).ToListAsync(_h.Ct);
        Assert.Equal(10, endpoints.Count);
        Assert.DoesNotContain(Device(1).Endpoint, endpoints);
    }

    [Fact]
    public async Task Users_remove_only_their_own_subscription()
    {
        var asha = await _h.ResidentAsync();
        var ravi = await _h.ResidentAsync();
        await Push.SubscribeAsync(asha, Device(1), _h.Ct);

        await Push.UnsubscribeAsync(ravi, new UnsubscribeRequest(Device(1).Endpoint), _h.Ct);
        Assert.Equal(1, await _h.As(Society.AceDivinoId).Db.PushSubscriptions.CountAsync(_h.Ct));

        await Push.UnsubscribeAsync(asha, new UnsubscribeRequest(Device(1).Endpoint), _h.Ct);
        Assert.Equal(0, await _h.As(Society.AceDivinoId).Db.PushSubscriptions.CountAsync(_h.Ct));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("http://insecure.example.com/push")]
    public async Task Invalid_subscriptions_are_refused(string endpoint)
    {
        var user = await _h.ResidentAsync();

        await Assert.ThrowsAsync<AppException>(() => Push.SubscribeAsync(user, new(endpoint, "k", "a"), _h.Ct));
    }

    public void Dispose() => _h.Dispose();
}
