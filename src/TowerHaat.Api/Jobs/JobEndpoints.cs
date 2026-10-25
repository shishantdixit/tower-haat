using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Orders;

namespace TowerHaat.Api.Jobs;

/// <summary>
/// Called by the hosting provider's scheduled tasks, which also wake the app if IIS unloaded it.
/// Protected by a shared key, not a user token. The key may come as the X-Job-Key header or, for
/// schedulers that can only open a URL, as ?key= on a GET.
/// </summary>
internal static class JobEndpoints
{
    public const string KeyHeader = "X-Job-Key";

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/api/jobs/expire-orders", ["GET", "POST"], async (HttpRequest request, IOptions<JobOptions> options, OrderExpiryJob job, CancellationToken ct) =>
        {
            if (!KeyMatches(options.Value.Key, GivenKey(request)))
            {
                return Results.NotFound();
            }

            return Results.Ok(new { expired = await job.RunAsync(ct) });
        });

        // Daily, from a second scheduled task; the app also makes one itself if a day passes without.
        app.MapMethods("/api/jobs/backup", ["GET", "POST"], async (HttpRequest request, IOptions<JobOptions> options, IDatabaseBackup backups, CancellationToken ct) =>
        {
            if (!KeyMatches(options.Value.Key, GivenKey(request)))
            {
                return Results.NotFound();
            }

            var backup = await backups.CreateAsync(ct);
            return Results.Ok(new { backup.Name, backup.SizeBytes });
        });

        return app;
    }

    private static string? GivenKey(HttpRequest request) =>
        request.Headers.TryGetValue(KeyHeader, out var header) ? header.ToString() : request.Query["key"].ToString();

    private static bool KeyMatches(string expected, string? given) =>
        expected.Length >= 32
        && given is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
}
