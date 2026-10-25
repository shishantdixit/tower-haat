using System.Security.Claims;
using TowerHaat.Api.Auth;
using TowerHaat.Application.Orders;
using TowerHaat.Application.Payments;

namespace TowerHaat.Api.Orders;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // C-06, C-07, C-09: the customer's orders.
        var mine = app.MapGroup("/api/orders").RequireAuthorization(AppClaims.ResidentPolicy);

        mine.MapPost("/", async (PlaceOrderRequest request, ClaimsPrincipal user, OrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.PlaceAsync(user.GetUserId(), request, ct)));

        mine.MapGet("/", async (ClaimsPrincipal user, OrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.ListMineAsync(user.GetUserId(), ct)));

        mine.MapGet("/{orderId:guid}", async (Guid orderId, ClaimsPrincipal user, OrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.GetMineAsync(user.GetUserId(), orderId, ct)));

        mine.MapPost("/{orderId:guid}/cancel", async (Guid orderId, ClaimsPrincipal user, OrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.CancelAsync(user.GetUserId(), orderId, ct)));

        mine.MapPost("/{orderId:guid}/paid", async (Guid orderId, MarkPaidRequest request, ClaimsPrincipal user, OrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.MarkPaidAsync(user.GetUserId(), orderId, request, ct)));

        // B-05, B-06, B-07, B-08: orders placed with the owner's business.
        var owner = app.MapGroup("/api/my/business").RequireAuthorization(AppClaims.ResidentPolicy);

        owner.MapGet("/orders", async (OwnerOrderTab? tab, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.ListAsync(user.GetUserId(), tab ?? OwnerOrderTab.New, ct)));

        owner.MapGet("/orders/{orderId:guid}", async (Guid orderId, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.GetAsync(user.GetUserId(), orderId, ct)));

        owner.MapPost("/orders/{orderId:guid}/accept", async (Guid orderId, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.AcceptAsync(user.GetUserId(), orderId, ct)));

        owner.MapPost("/orders/{orderId:guid}/reject", async (Guid orderId, ReasonRequest request, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.RejectAsync(user.GetUserId(), orderId, request, ct)));

        owner.MapPost("/orders/{orderId:guid}/advance", async (Guid orderId, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.AdvanceAsync(user.GetUserId(), orderId, ct)));

        owner.MapPost("/orders/{orderId:guid}/cancel", async (Guid orderId, ReasonRequest request, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.CancelAsync(user.GetUserId(), orderId, request, ct)));

        owner.MapPost("/orders/{orderId:guid}/payment", async (Guid orderId, PaymentActionRequest request, ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.UpdatePaymentAsync(user.GetUserId(), orderId, request, ct)));

        owner.MapGet("/prep-summary", async (ClaimsPrincipal user, OwnerOrderService orders, CancellationToken ct) =>
            TypedResults.Ok(await orders.PrepSummaryAsync(user.GetUserId(), ct)));

        return app;
    }
}
