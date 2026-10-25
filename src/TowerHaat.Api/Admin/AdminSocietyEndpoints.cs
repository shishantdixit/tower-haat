using TowerHaat.Api.Auth;
using TowerHaat.Application.Societies;

namespace TowerHaat.Api.Admin;

internal static class AdminSocietyEndpoints
{
    public static IEndpointRouteBuilder MapAdminSocietyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/societies").RequireAuthorization(AppClaims.PlatformAdminPolicy);

        // A-01
        group.MapGet("/", async (SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.ListAsync(ct)));

        group.MapPost("/", async (CreateSocietyRequest request, SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.CreateAsync(request, ct)));

        group.MapPost("/{societyId:guid}/activate", async (Guid societyId, SocietyAdminService admin, CancellationToken ct) =>
        {
            await admin.SetActiveAsync(societyId, isActive: true, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{societyId:guid}/deactivate", async (Guid societyId, SocietyAdminService admin, CancellationToken ct) =>
        {
            await admin.SetActiveAsync(societyId, isActive: false, ct);
            return TypedResults.NoContent();
        });

        // A-06
        group.MapGet("/{societyId:guid}/towers", async (Guid societyId, SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.ListTowersAsync(societyId, ct)));

        group.MapPost("/{societyId:guid}/towers", async (Guid societyId, CreateTowerRequest request, SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.AddTowerAsync(societyId, request, ct)));

        group.MapDelete("/{societyId:guid}/towers/{towerId:guid}", async (Guid societyId, Guid towerId, SocietyAdminService admin, CancellationToken ct) =>
        {
            await admin.DeleteTowerAsync(societyId, towerId, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{societyId:guid}/towers/{towerId:guid}/flats", async (Guid societyId, Guid towerId, CreateFlatRequest request, SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.AddFlatAsync(societyId, towerId, request, ct)));

        group.MapDelete("/{societyId:guid}/flats/{flatId:guid}", async (Guid societyId, Guid flatId, SocietyAdminService admin, CancellationToken ct) =>
        {
            await admin.DeleteFlatAsync(societyId, flatId, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{societyId:guid}/flats/import", async (Guid societyId, ImportFlatsRequest request, SocietyAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.ImportFlatsAsync(societyId, request, ct)));

        return app;
    }
}
