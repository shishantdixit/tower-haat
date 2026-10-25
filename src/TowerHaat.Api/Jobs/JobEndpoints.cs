using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Orders;

namespace TowerHaat.Api.Jobs;

/// <summary>
/// Called every few minutes by the MonsterASP scheduled task, which also wakes the app if IIS
/// unloaded it. Protected by a shared key, not by a user token.
/// </summary>
internal static class JobEndpoints
{
    public const string KeyHeader = "X-Job-Key";

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jobs/expire-orders", async (HttpRequest request, IOptions<JobOptions> options, OrderExpiryJob job, CancellationToken ct) =>
        {
            if (!KeyMatches(options.Value.Key, request.Headers[KeyHeader]))
            {
                return Results.NotFound();
            }

            return Results.Ok(new { expired = await job.RunAsync(ct) });
        });

        return app;
    }

    private static bool KeyMatches(string expected, string? given) =>
        expected.Length >= 32
        && given is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
}
