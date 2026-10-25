using System.Security.Claims;

namespace TowerHaat.Api.Auth;

public static class AppClaims
{
    public const string UserId = "sub";
    public const string Phone = "phone";
    public const string SocietyId = "society_id";
    public const string Role = "role";
    public const string PlatformAdminRole = "platform_admin";

    public const string PlatformAdminPolicy = "PlatformAdmin";

    public static Guid GetUserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(UserId)!);

    public static Guid? GetSocietyId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SocietyId), out var id) ? id : null;
}
