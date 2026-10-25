using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace TowerHaat.Api.Hosting;

/// <summary>
/// Cache rules for the Angular build. Hashed bundles never change, so phones keep them for a year;
/// index.html and the service worker files must always be checked, or phones would never see a new
/// version of the app.
/// </summary>
internal static partial class StaticFileCaching
{
    public const string Immutable = "public, max-age=31536000, immutable";
    public const string AlwaysCheck = "no-cache";
    public const string OneDay = "public, max-age=86400";

    public static string For(string path)
    {
        var file = Path.GetFileName(path);
        if (file is "" or "index.html" or "ngsw.json" or "ngsw-worker.js" or "safety-worker.js" or "worker-basic.min.js" or "manifest.webmanifest")
        {
            return AlwaysCheck;
        }

        return HashedBundle().IsMatch(file) ? Immutable : OneDay;
    }

    public static StaticFileOptions Options() => new()
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers[HeaderNames.CacheControl] = For(context.File.Name),
    };

    // main-JPRHA27N.js, chunk-BDZSi8mb.js, styles-TFXOPEMF.css
    [GeneratedRegex(@"^[\w-]+-[A-Za-z0-9]{8}\.(js|css)$")]
    private static partial Regex HashedBundle();
}
