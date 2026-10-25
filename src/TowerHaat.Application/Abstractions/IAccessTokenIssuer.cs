namespace TowerHaat.Application.Abstractions;

public sealed record AccessTokenClaims(Guid UserId, string Phone, Guid? SocietyId, bool IsPlatformAdmin);

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenClaims claims);
}
