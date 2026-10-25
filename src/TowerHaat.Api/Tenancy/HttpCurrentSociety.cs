using TowerHaat.Application.Abstractions;

namespace TowerHaat.Api.Tenancy;

/// <summary>Reads the society from the signed-in user's token (issued in M1).</summary>
internal sealed class HttpCurrentSociety(IHttpContextAccessor httpContextAccessor) : ICurrentSociety
{
    public const string ClaimType = "society_id";

    public Guid? SocietyId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(ClaimType)?.Value, out var id) ? id : null;
}
