using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Residents;
using TowerHaat.Application.Tenancy;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Tests.Support;

/// <summary>Builds the auth and profile services over a test database, as one request would see them.</summary>
public sealed class AuthHarness : IDisposable
{
    public const string AdminPhone = "+919999900000";

    public AuthHarness()
    {
        Options = new AuthOptions
        {
            AdminPhones = [AdminPhone],
            Otp = new OtpOptions { HashKey = new string('k', 32) },
        };
    }

    public TestDatabase Database { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 25, 6, 0, 0, TimeSpan.Zero));

    public CapturingSmsSender Sms { get; } = new();

    public FakeAccessTokenIssuer Tokens { get; } = new();

    public AuthOptions Options { get; }

    /// <summary>A fresh context and services, like a new HTTP request.</summary>
    public (AuthService Auth, MeService Me, AppDbContext Db) Request(Guid? societyId = null)
    {
        var society = TestDatabase.Society(societyId);
        var db = Database.CreateContext(society);
        var me = new MeService(db, society, Tokens, Time);
        var auth = new AuthService(db, Sms, me, Microsoft.Extensions.Options.Options.Create(Options), Time);
        return (auth, me, db);
    }

    public async Task<AuthResult> SignInAsync(string phone)
    {
        var ct = TestContext.Current.CancellationToken;
        await Request().Auth.RequestOtpAsync(new RequestOtpRequest(phone), ct);
        var result = await Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(phone, Sms.LastCodeFor(Normalize(phone))), ct);
        Time.Advance(TimeSpan.FromMinutes(1));
        return result;
    }

    private static string Normalize(string phone) =>
        TowerHaat.Domain.Users.PhoneNumber.TryNormalize(phone, out var normalized) ? normalized : phone;

    public void Dispose() => Database.Dispose();
}
