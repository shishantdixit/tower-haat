using TowerHaat.Application.Societies;

namespace TowerHaat.Api.Societies;

/// <summary>Public: the sign-up picker needs these before the user has a society.</summary>
internal static class SocietyEndpoints
{
    public static IEndpointRouteBuilder MapSocietyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/societies");

        group.MapGet("/", async (SocietyDirectoryService directory, CancellationToken ct) =>
            TypedResults.Ok(await directory.ListActiveAsync(ct)));

        group.MapGet("/{societyId:guid}/towers", async (Guid societyId, SocietyDirectoryService directory, CancellationToken ct) =>
            TypedResults.Ok(await directory.ListTowersAsync(societyId, ct)));

        return app;
    }
}
