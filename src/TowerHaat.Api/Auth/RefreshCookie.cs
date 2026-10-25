namespace TowerHaat.Api.Auth;

/// <summary>The refresh token lives in an HTTP-only cookie scoped to /api/auth, out of reach of scripts.</summary>
internal static class RefreshCookie
{
    public const string Name = "th_refresh";
    private const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTime expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expiresAt: null));

    private static CookieOptions Options(DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
