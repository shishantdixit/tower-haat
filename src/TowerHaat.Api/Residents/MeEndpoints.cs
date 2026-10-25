using System.Security.Claims;
using TowerHaat.Api.Auth;
using TowerHaat.Application.Residents;

namespace TowerHaat.Api.Residents;

internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, MeService me, CancellationToken ct) =>
            TypedResults.Ok(await me.GetAsync(user.GetUserId(), ct)));

        group.MapPut("/profile", async (SaveProfileRequest request, ClaimsPrincipal user, MeService me, CancellationToken ct) =>
            TypedResults.Ok(await me.SaveProfileAsync(user.GetUserId(), request, ct)));

        group.MapDelete("/", async (ClaimsPrincipal user, MeService me, HttpResponse response, CancellationToken ct) =>
        {
            await me.DeleteAccountAsync(user.GetUserId(), ct);
            RefreshCookie.Delete(response);
            return TypedResults.NoContent();
        });

        return app;
    }
}
