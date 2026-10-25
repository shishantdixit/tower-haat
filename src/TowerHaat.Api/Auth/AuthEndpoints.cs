using TowerHaat.Application.Auth;

namespace TowerHaat.Api.Auth;

internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // Only code requests and checks are limited per IP. Refresh runs on every app load, and a
        // whole society can share one public IP, so limiting it would sign people out.
        group.MapPost("/otp", async (RequestOtpRequest request, AuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.RequestOtpAsync(request, ct)))
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/verify", async (VerifyOtpRequest request, AuthService auth, HttpResponse response, CancellationToken ct) =>
                TypedResults.Ok(SignIn(response, await auth.VerifyOtpAsync(request, ct))))
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/refresh", async (HttpRequest request, AuthService auth, HttpResponse response, CancellationToken ct) =>
        {
            try
            {
                return TypedResults.Ok(SignIn(response, await auth.RefreshAsync(RefreshCookie.Read(request), ct)));
            }
            catch
            {
                RefreshCookie.Delete(response);
                throw;
            }
        });

        group.MapPost("/logout", async (HttpRequest request, AuthService auth, HttpResponse response, CancellationToken ct) =>
        {
            await auth.LogoutAsync(RefreshCookie.Read(request), ct);
            RefreshCookie.Delete(response);
            return TypedResults.NoContent();
        });

        return app;
    }

    private static AuthResponse SignIn(HttpResponse response, AuthResult result)
    {
        RefreshCookie.Write(response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return new AuthResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }
}
