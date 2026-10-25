using TowerHaat.Api.Auth;
using TowerHaat.Application.Businesses;

namespace TowerHaat.Api.Businesses;

internal static class BusinessTypeEndpoints
{
    public static IEndpointRouteBuilder MapBusinessTypeEndpoints(this IEndpointRouteBuilder app)
    {
        // Active types and categories, for registration.
        app.MapGet("/api/business-types", async (BusinessTypeService types, CancellationToken ct) =>
                TypedResults.Ok(await types.ListAsync(includeInactive: false, ct)))
            .RequireAuthorization();

        // A-03
        var admin = app.MapGroup("/api/admin/business-types").RequireAuthorization(AppClaims.PlatformAdminPolicy);

        admin.MapGet("/", async (BusinessTypeService types, CancellationToken ct) =>
            TypedResults.Ok(await types.ListAsync(includeInactive: true, ct)));

        admin.MapPost("/", async (SaveBusinessTypeRequest request, BusinessTypeService types, CancellationToken ct) =>
            TypedResults.Ok(await types.CreateTypeAsync(request, ct)));

        admin.MapPut("/{typeId:guid}", async (Guid typeId, SaveBusinessTypeRequest request, BusinessTypeService types, CancellationToken ct) =>
        {
            await types.UpdateTypeAsync(typeId, request, ct);
            return TypedResults.NoContent();
        });

        admin.MapPost("/{typeId:guid}/categories", async (Guid typeId, SaveCategoryRequest request, BusinessTypeService types, CancellationToken ct) =>
            TypedResults.Ok(await types.CreateCategoryAsync(typeId, request, ct)));

        admin.MapPut("/categories/{categoryId:guid}", async (Guid categoryId, SaveCategoryRequest request, BusinessTypeService types, CancellationToken ct) =>
        {
            await types.UpdateCategoryAsync(categoryId, request, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
