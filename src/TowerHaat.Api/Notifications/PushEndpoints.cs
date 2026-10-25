using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TowerHaat.Api.Auth;
using TowerHaat.Application.Notifications;

namespace TowerHaat.Api.Notifications;

internal static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/push").RequireAuthorization();

        // 404 when push is not configured, so the app hides the "turn on notifications" card.
        group.MapGet("/public-key", (IOptions<PushOptions> options) =>
            options.Value.IsConfigured ? Results.Ok(new { publicKey = options.Value.PublicKey }) : Results.NotFound());

        group.MapPost("/subscriptions", async (SubscribeRequest request, ClaimsPrincipal user, PushSubscriptionService subscriptions, CancellationToken ct) =>
        {
            await subscriptions.SubscribeAsync(user.GetUserId(), request, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/subscriptions/remove", async (UnsubscribeRequest request, ClaimsPrincipal user, PushSubscriptionService subscriptions, CancellationToken ct) =>
        {
            await subscriptions.UnsubscribeAsync(user.GetUserId(), request, ct);
            return TypedResults.NoContent();
        });

        return app;
    }

    /// <summary><c>dotnet run --project src/TowerHaat.Api -- generate-vapid-keys</c> prints a new VAPID key pair.</summary>
    public static void PrintNewVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        byte[] publicKey = [0x04, .. p.Q.X!, .. p.Q.Y!];
        Console.WriteLine($"Push__PublicKey={Base64Url(publicKey)}");
        Console.WriteLine($"Push__PrivateKey={Base64Url(p.D!)}");
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
