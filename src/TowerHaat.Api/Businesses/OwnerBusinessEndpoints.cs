using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TowerHaat.Api.Auth;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Media;

namespace TowerHaat.Api.Businesses;

/// <summary>The signed-in resident's own business and catalog (B-01, B-02, B-03).</summary>
internal static class OwnerBusinessEndpoints
{
    public static IEndpointRouteBuilder MapOwnerBusinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/my/business").RequireAuthorization(AppClaims.ResidentPolicy);

        group.MapGet("/", async (ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            TypedResults.Ok(await owner.GetMineAsync(user.GetUserId(), ct)));

        group.MapPost("/", async (SaveBusinessRequest request, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            TypedResults.Ok(await owner.RegisterAsync(user.GetUserId(), request, ct)));

        group.MapPut("/", async (SaveBusinessRequest request, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            TypedResults.Ok(await owner.UpdateProfileAsync(user.GetUserId(), request, ct)));

        group.MapPut("/settings", async (BusinessSettingsRequest request, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            TypedResults.Ok(await owner.UpdateSettingsAsync(user.GetUserId(), request, ct)));

        group.MapPost("/open", async (SetOpenRequest request, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            TypedResults.Ok(await owner.SetOpenAsync(user.GetUserId(), request.IsOpen, ct)));

        group.MapPost("/photos", async (IFormFile file, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return TypedResults.Ok(await owner.AddPhotoAsync(user.GetUserId(), stream, file.Length, ct));
            })
            .AcceptsPhoto();

        group.MapDelete("/photos/{photoId:guid}", async (Guid photoId, ClaimsPrincipal user, OwnerBusinessService owner, CancellationToken ct) =>
        {
            await owner.DeletePhotoAsync(user.GetUserId(), photoId, ct);
            return TypedResults.NoContent();
        });

        var items = group.MapGroup("/items");

        items.MapGet("/", async (ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            TypedResults.Ok(await catalog.ListAsync(user.GetUserId(), ct)));

        items.MapPost("/", async (SaveItemRequest request, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            TypedResults.Ok(await catalog.CreateAsync(user.GetUserId(), request, ct)));

        items.MapPut("/{itemId:guid}", async (Guid itemId, SaveItemRequest request, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            TypedResults.Ok(await catalog.UpdateAsync(user.GetUserId(), itemId, request, ct)));

        items.MapPost("/{itemId:guid}/availability", async (Guid itemId, SetAvailabilityRequest request, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            TypedResults.Ok(await catalog.SetAvailabilityAsync(user.GetUserId(), itemId, request.IsAvailableToday, ct)));

        items.MapDelete("/{itemId:guid}", async (Guid itemId, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
        {
            await catalog.DeleteAsync(user.GetUserId(), itemId, ct);
            return TypedResults.NoContent();
        });

        items.MapPost("/{itemId:guid}/photo", async (Guid itemId, IFormFile file, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return TypedResults.Ok(await catalog.SetPhotoAsync(user.GetUserId(), itemId, stream, file.Length, ct));
            })
            .AcceptsPhoto();

        items.MapDelete("/{itemId:guid}/photo", async (Guid itemId, ClaimsPrincipal user, CatalogService catalog, CancellationToken ct) =>
            TypedResults.Ok(await catalog.RemovePhotoAsync(user.GetUserId(), itemId, ct)));

        return app;
    }

    /// <summary>
    /// Multipart photo upload. Antiforgery is off because the API uses bearer tokens, not cookies;
    /// the body limit leaves room for the 2 MB photo plus form overhead.
    /// </summary>
    private static RouteHandlerBuilder AcceptsPhoto(this RouteHandlerBuilder builder) =>
        builder
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ImageUpload.MaxBytes + (64 * 1024)));
}
