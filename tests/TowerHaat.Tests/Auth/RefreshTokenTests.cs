using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Common;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Auth;

public sealed class RefreshTokenTests : IDisposable
{
    private const string Phone = "+919876543210";
    private readonly AuthHarness _harness = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var signIn = await _harness.SignInAsync(Phone);

        var refreshed = await _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct);

        Assert.NotEqual(signIn.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(signIn.User.Id, refreshed.User.Id);
        await _harness.Request().Auth.RefreshAsync(refreshed.RefreshToken, _ct);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_ends_every_session()
    {
        var signIn = await _harness.SignInAsync(Phone);
        var refreshed = await _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct);

        await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct));

        // The legitimate newer token is revoked too.
        await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(refreshed.RefreshToken, _ct));
    }

    [Fact]
    public async Task Expired_token_is_refused()
    {
        var signIn = await _harness.SignInAsync(Phone);
        _harness.Time.Advance(_harness.Options.RefreshTokenLifetime);

        var error = await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct));
        Assert.Equal(ErrorKind.Unauthorized, error.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Unknown_token_is_refused(string? token)
    {
        await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(token, _ct));
    }

    [Fact]
    public async Task Logout_revokes_the_token()
    {
        var signIn = await _harness.SignInAsync(Phone);

        await _harness.Request().Auth.LogoutAsync(signIn.RefreshToken, _ct);

        await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct));
    }

    [Fact]
    public async Task Blocked_user_cannot_refresh()
    {
        var signIn = await _harness.SignInAsync(Phone);
        var (_, _, db) = _harness.Request();
        (await db.Users.SingleAsync(_ct)).Block();
        await db.SaveChangesAsync(_ct);

        await Assert.ThrowsAsync<AppException>(() => _harness.Request().Auth.RefreshAsync(signIn.RefreshToken, _ct));
    }

    [Fact]
    public async Task Only_a_hash_of_the_token_is_stored()
    {
        var signIn = await _harness.SignInAsync(Phone);

        var stored = await _harness.Request().Db.RefreshTokens.SingleAsync(_ct);
        Assert.NotEqual(signIn.RefreshToken, stored.TokenHash);
    }

    public void Dispose() => _harness.Dispose();
}
