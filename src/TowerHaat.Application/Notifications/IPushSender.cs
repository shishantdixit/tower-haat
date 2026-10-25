namespace TowerHaat.Application.Notifications;

public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

public enum PushResult
{
    Sent,

    /// <summary>The push service says the subscription no longer exists; delete it.</summary>
    Gone,

    Failed,
}

/// <summary>Sends one web push message (encrypted, VAPID-signed) to one subscription.</summary>
public interface IPushSender
{
    bool IsConfigured { get; }

    Task<PushResult> SendAsync(PushTarget target, string payloadJson, string? topic, CancellationToken cancellationToken);
}

public sealed class PushOptions
{
    public const string Section = "Push";

    /// <summary>VAPID public key, base64url; given to browsers when they subscribe.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key, base64url. Secret.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>Contact for push services, mailto: or https:.</summary>
    public string Subject { get; set; } = "mailto:hello@towerhaat.com";

    public bool IsConfigured => PublicKey.Length > 0 && PrivateKey.Length > 0;
}
