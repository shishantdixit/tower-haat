using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Auth;

/// <summary>
/// A long-lived sign-in, rotated on every use. Only a hash of the token is stored.
/// Presenting a token that was already rotated means it leaked, so every token of the user is revoked.
/// </summary>
public class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTime now) => RevokedAt ??= now;

    public void ReplaceWith(RefreshToken next, DateTime now)
    {
        Revoke(now);
        ReplacedById = next.Id;
    }
}
