using TowerHaat.Api.Auth;
using TowerHaat.Application.Businesses;
using TowerHaat.Domain.Businesses;

namespace TowerHaat.Api.Businesses;

/// <summary>A-02: the approval queue.</summary>
internal static class AdminBusinessEndpoints
{
    public static IEndpointRouteBuilder MapAdminBusinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/businesses").RequireAuthorization(AppClaims.PlatformAdminPolicy);

        group.MapGet("/", async (BusinessStatus? status, BusinessAdminService admin, CancellationToken ct) =>
            TypedResults.Ok(await admin.ListAsync(status, ct)));

        group.MapPost("/{businessId:guid}/approve", async (Guid businessId, BusinessDecisionRequest request, BusinessAdminService admin, CancellationToken ct) =>
        {
            await admin.ApproveAsync(businessId, request, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{businessId:guid}/reject", async (Guid businessId, BusinessDecisionRequest request, BusinessAdminService admin, CancellationToken ct) =>
        {
            await admin.RejectAsync(businessId, request, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{businessId:guid}/deactivate", async (Guid businessId, BusinessDecisionRequest request, BusinessAdminService admin, CancellationToken ct) =>
        {
            await admin.DeactivateAsync(businessId, request, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
