using System.Security.Claims;
using TowerHaat.Api.Auth;
using TowerHaat.Application.Directory;

namespace TowerHaat.Api.Directory;

/// <summary>C-02, C-03, C-04: what residents browse in their own society.</summary>
internal static class DirectoryEndpoints
{
    public static IEndpointRouteBuilder MapDirectoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/directory").RequireAuthorization(AppClaims.ResidentPolicy);

        group.MapGet("/businesses", async (DirectoryService directory, CancellationToken ct) =>
            TypedResults.Ok(await directory.ListAsync(ct)));

        group.MapGet("/businesses/{businessId:guid}", async (Guid businessId, ClaimsPrincipal user, DirectoryService directory, CancellationToken ct) =>
            TypedResults.Ok(await directory.GetAsync(user.GetUserId(), businessId, ct)));

        return app;
    }
}
