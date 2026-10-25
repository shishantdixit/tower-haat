namespace TowerHaat.Api.Health;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");

        group.MapGet("/", () => TypedResults.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)));

        return app;
    }

    internal sealed record HealthResponse(string Status, DateTimeOffset UtcNow);
}
