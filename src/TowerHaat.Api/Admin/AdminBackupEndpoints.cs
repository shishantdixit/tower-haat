using TowerHaat.Api.Auth;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Api.Admin;

/// <summary>The platform admin lists and downloads backups, to keep copies away from the server.</summary>
internal static class AdminBackupEndpoints
{
    public static IEndpointRouteBuilder MapAdminBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/backups").RequireAuthorization(AppClaims.PlatformAdminPolicy);

        group.MapGet("/", (IDatabaseBackup backups) => TypedResults.Ok(backups.List()));

        group.MapPost("/", async (IDatabaseBackup backups, CancellationToken ct) => TypedResults.Ok(await backups.CreateAsync(ct)));

        group.MapGet("/{name}", (string name, IDatabaseBackup backups) =>
            backups.Open(name) is { } stream
                ? Results.File(stream, "application/zip", name)
                : Results.NotFound());

        return app;
    }
}
