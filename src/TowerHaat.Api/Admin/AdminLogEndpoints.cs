using TowerHaat.Api.Auth;
using TowerHaat.Api.Hosting;
using TowerHaat.Application.Admin;

namespace TowerHaat.Api.Admin;

/// <summary>Recent warnings and errors for the platform admin, readable from a phone.</summary>
internal static class AdminLogEndpoints
{
    public const int MaxLines = 300;

    public static IEndpointRouteBuilder MapAdminLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/logs/errors", (IConfiguration configuration, IHostEnvironment environment) =>
            {
                var directory = LogOptions.Directory(configuration, environment);
                return TypedResults.Ok(new { lines = RecentLines(directory) });
            })
            .RequireAuthorization(AppClaims.PlatformAdminPolicy);

        // Pilot measures (from the development plan's pilot test).
        app.MapGet("/api/admin/stats", async (int? days, PilotStatsService stats, CancellationToken ct) =>
                TypedResults.Ok(await stats.GetAsync(days ?? 14, ct)))
            .RequireAuthorization(AppClaims.PlatformAdminPolicy);

        return app;
    }

    /// <summary>The newest lines of the newest error files, newest first.</summary>
    internal static List<string> RecentLines(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var file in System.IO.Directory.GetFiles(directory, $"{LoggingSetup.ErrorFilePrefix}*.log").OrderDescending())
        {
            // The logger keeps the file open; read it shared.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var fileLines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                fileLines.Add(line);
            }

            fileLines.Reverse();
            lines.AddRange(fileLines);
            if (lines.Count >= MaxLines)
            {
                break;
            }
        }

        return lines.Take(MaxLines).ToList();
    }
}
