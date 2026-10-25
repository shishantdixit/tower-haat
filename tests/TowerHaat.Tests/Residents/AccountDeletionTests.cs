using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Common;
using TowerHaat.Application.Notifications;
using TowerHaat.Application.Residents;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Residents;

/// <summary>DPDP: a person can delete their account; their past orders keep their snapshots.</summary>
public sealed class AccountDeletionTests : IDisposable
{
    private const string Phone = "+919876543210";
    private readonly AuthHarness _auth = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<Guid> SignedUpAsync()
    {
        var signIn = await _auth.SignInAsync(Phone);
        await _auth.Request().Me.SaveProfileAsync(
            signIn.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, null, "A", "101", true), _ct);
        return signIn.User.Id;
    }

    [Fact]
    public async Task Deleting_removes_the_profile_sessions_and_devices_and_frees_the_phone()
    {
        var userId = await SignedUpAsync();
        await new PushSubscriptionService(_auth.Request().Db, _auth.Time).SubscribeAsync(
            userId, new SubscribeRequest("https://push.example.com/1", "k", "a"), _ct);

        await _auth.Request(Society.AceDivinoId).Me.DeleteAccountAsync(userId, _ct);

        var db = _auth.Request().Db;
        var user = await db.Users.SingleAsync(u => u.Id == userId, _ct);
        Assert.NotNull(user.DeletedAt);
        Assert.Null(user.Name);
        Assert.StartsWith("#", user.Phone);
        Assert.Equal(0, await db.ResidentProfiles.IgnoreQueryFilters().CountAsync(_ct));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(_ct));
        Assert.Equal(0, await db.PushSubscriptions.CountAsync(_ct));
        Assert.Equal(0, await db.OtpRequests.CountAsync(_ct));

        // The same phone can sign up again, as a new person.
        var again = await _auth.SignInAsync(Phone);
        Assert.NotEqual(userId, again.User.Id);
        Assert.Null(again.User.Profile);
    }

    [Fact]
    public async Task Deleted_account_cannot_keep_using_its_session()
    {
        var userId = await SignedUpAsync();
        var refresh = (await _auth.SignInAsync(Phone)).RefreshToken;

        await _auth.Request(Society.AceDivinoId).Me.DeleteAccountAsync(userId, _ct);

        await Assert.ThrowsAsync<AppException>(() => _auth.Request().Auth.RefreshAsync(refresh, _ct));
        await Assert.ThrowsAsync<AppException>(() => _auth.Request().Me.GetAsync(userId, _ct));
    }

    public void Dispose() => _auth.Dispose();
}

/// <summary>Deleting is refused while it would break a business or an order in progress.</summary>
public sealed class AccountDeletionGuardTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    [Fact]
    public async Task Business_owner_must_close_the_business_first()
    {
        var (owner, _) = await _h.BusinessAsync();
        var me = new MeService(_h.As(Society.AceDivinoId).Db, TestDatabase.Society(Society.AceDivinoId), new FakeAccessTokenIssuer(), _h.Time);

        var error = await Assert.ThrowsAsync<AppException>(() => me.DeleteAccountAsync(owner, _h.Ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Open_orders_must_finish_first_and_past_orders_keep_their_snapshot()
    {
        var kitchen = await Kitchen.OpenAsync(_h);
        var customer = await _h.ResidentAsync();
        var order = await kitchen.OrderAsync(customer, (kitchen.Thali, 1));
        MeService Me() => new(_h.As(Society.AceDivinoId).Db, TestDatabase.Society(Society.AceDivinoId), new FakeAccessTokenIssuer(), _h.Time);

        await Assert.ThrowsAsync<AppException>(() => Me().DeleteAccountAsync(customer, _h.Ct));

        await kitchen.Owner.RejectAsync(kitchen.OwnerId, order.Id, new("Closed"), _h.Ct);
        await Me().DeleteAccountAsync(customer, _h.Ct);

        var kept = await kitchen.Owner.GetAsync(kitchen.OwnerId, order.Id, _h.Ct);
        Assert.Equal(("Resident 2", "A"), (kept.CustomerName, kept.Tower));
    }

    public void Dispose() => _h.Dispose();
}
