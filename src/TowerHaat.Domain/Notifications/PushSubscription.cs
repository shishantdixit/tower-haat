using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Notifications;

/// <summary>
/// One browser or installed app that receives web push for a user. Belongs to the user, who is
/// shared across societies, so it is not a tenant entity.
/// </summary>
public class PushSubscription : Entity
{
    private PushSubscription()
    {
    }

    public PushSubscription(Guid userId, string endpoint, string endpointHash, string p256dh, string auth, DateTime now)
    {
        Endpoint = endpoint;
        EndpointHash = endpointHash;
        CreatedAt = now;
        Renew(userId, p256dh, auth, now);
    }

    public Guid UserId { get; private set; }

    /// <summary>The push service URL; secret to the browser that created it.</summary>
    public string Endpoint { get; private set; } = null!;

    /// <summary>SHA-256 of the endpoint, for a unique index (endpoints are too long to index).</summary>
    public string EndpointHash { get; private set; } = null!;

    public string P256dh { get; private set; } = null!;

    public string Auth { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>The same browser subscribing again, possibly after another user signed in on it.</summary>
    public void Renew(Guid userId, string p256dh, string auth, DateTime now)
    {
        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
        UpdatedAt = now;
    }
}
