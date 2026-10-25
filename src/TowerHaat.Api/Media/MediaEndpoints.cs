using Microsoft.Net.Http.Headers;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Media;

namespace TowerHaat.Api.Media;

/// <summary>
/// Serves uploaded photos. Public, because image tags cannot send a bearer token; keys are random,
/// and a photo never changes once stored (a new upload gets a new key), so it caches for a year.
/// </summary>
internal static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/{year}/{month}/{file}", (string year, string month, string file, IFileStorage files, HttpResponse response) =>
        {
            var key = $"{year}/{month}/{file}";
            var stream = files.OpenRead(key);
            if (stream is null)
            {
                return Results.NotFound();
            }

            response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
            return Results.Stream(stream, ImageUpload.ContentType(key));
        });

        return app;
    }
}
