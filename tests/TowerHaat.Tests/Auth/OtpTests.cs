using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Common;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Auth;

/// <summary>OTP rules: expiry, wrong-code attempts, per-phone rate limits (C-01).</summary>
public sealed class OtpTests : IDisposable
{
    private const string Phone = "+919876543210";
    private readonly AuthHarness _harness = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Requesting_a_code_sends_it_and_stores_only_a_hash()
    {
        var response = await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest("98765 43210"), _ct);

        var (phone, code) = Assert.Single(_harness.Sms.Sent);
        Assert.Equal(Phone, phone);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Null(response.DevelopmentCode);

        var stored = await _harness.Request().Db.OtpRequests.SingleAsync(_ct);
        Assert.DoesNotContain(code, stored.CodeHash);
    }

    [Fact]
    public async Task Invalid_phone_is_rejected()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest("12345"), _ct));

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Empty(_harness.Sms.Sent);
    }

    [Fact]
    public async Task Correct_code_signs_in_and_creates_the_user()
    {
        var result = await _harness.SignInAsync("9876543210");

        Assert.Equal(Phone, result.User.Phone);
        Assert.Null(result.User.Profile);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(1, await _harness.Request().Db.Users.CountAsync(_ct));
    }

    [Fact]
    public async Task Signing_in_again_reuses_the_same_user()
    {
        var first = await _harness.SignInAsync(Phone);
        var second = await _harness.SignInAsync(Phone);

        Assert.Equal(first.User.Id, second.User.Id);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        var code = _harness.Sms.LastCodeFor(Phone);
        await _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, code), _ct);

        await Assert.ThrowsAsync<AppException>(
            () => _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, code), _ct));
    }

    [Fact]
    public async Task A_code_expires_after_its_lifetime()
    {
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        _harness.Time.Advance(_harness.Options.Otp.Lifetime);

        var error = await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.VerifyOtpAsync(
            new VerifyOtpRequest(Phone, _harness.Sms.LastCodeFor(Phone)), _ct));

        Assert.Contains("expired", error.Message);
    }

    [Fact]
    public async Task Too_many_wrong_codes_lock_the_code()
    {
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        var code = _harness.Sms.LastCodeFor(Phone);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 1; i <= _harness.Options.Otp.MaxAttempts; i++)
        {
            var error = await Assert.ThrowsAsync<AppException>(
                () => _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, wrong), _ct));
            Assert.Contains("Wrong code", error.Message);
        }

        // Even the right code no longer works.
        var locked = await Assert.ThrowsAsync<AppException>(
            () => _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, code), _ct));
        Assert.Contains("expired", locked.Message);
    }

    [Fact]
    public async Task Only_the_latest_code_counts()
    {
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        var first = _harness.Sms.LastCodeFor(Phone);
        _harness.Time.Advance(_harness.Options.Otp.ResendCooldown);
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        var second = _harness.Sms.LastCodeFor(Phone);

        if (first != second)
        {
            await Assert.ThrowsAsync<AppException>(
                () => _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, first), _ct));
        }

        await _harness.Request().Auth.VerifyOtpAsync(new VerifyOtpRequest(Phone, second), _ct);
    }

    [Fact]
    public async Task Resending_within_the_cooldown_is_refused()
    {
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        _harness.Time.Advance(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<AppException>(
            () => _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct));

        Assert.Equal(ErrorKind.TooManyRequests, error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(20), error.RetryAfter);
        Assert.Single(_harness.Sms.Sent);
    }

    [Fact]
    public async Task At_most_five_codes_per_phone_per_hour()
    {
        for (var i = 0; i < _harness.Options.Otp.MaxRequestsPerHour; i++)
        {
            await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
            _harness.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var error = await Assert.ThrowsAsync<AppException>(
            () => _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct));
        Assert.Equal(ErrorKind.TooManyRequests, error.Kind);

        // Another phone is not affected, and the limit lifts after an hour.
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest("+919876500000"), _ct);
        _harness.Time.Advance(TimeSpan.FromMinutes(56));
        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
    }

    [Fact]
    public async Task Configured_admin_phone_becomes_platform_admin()
    {
        var admin = await _harness.SignInAsync(AuthHarness.AdminPhone);
        var resident = await _harness.SignInAsync(Phone);

        Assert.True(admin.User.IsPlatformAdmin);
        Assert.True(_harness.Tokens.Last is not null);
        Assert.False(resident.User.IsPlatformAdmin);
    }

    [Fact]
    public async Task Blocked_user_cannot_sign_in()
    {
        var result = await _harness.SignInAsync(Phone);
        var (_, _, db) = _harness.Request();
        (await db.Users.SingleAsync(u => u.Id == result.User.Id, _ct)).Block();
        await db.SaveChangesAsync(_ct);

        await _harness.Request().Auth.RequestOtpAsync(new RequestOtpRequest(Phone), _ct);
        var error = await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.VerifyOtpAsync(
            new VerifyOtpRequest(Phone, _harness.Sms.LastCodeFor(Phone)), _ct));

        Assert.Equal(ErrorKind.Forbidden, error.Kind);
    }

    public void Dispose() => _harness.Dispose();
}
