using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Notifications;

namespace TowerHaat.Application.Notifications;

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth);

public sealed record UnsubscribeRequest(string Endpoint);

/// <summary>Devices that receive web push for the signed-in user.</summary>
public sealed class PushSubscriptionService(IAppDbContext db, TimeProvider time)
{
    public const int MaxDevicesPerUser = 10;

    /// <summary>Saves the browser's subscription. The same browser re-subscribing (or another user signing in on it) updates it.</summary>
    public async Task SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken cancellationToken)
    {
        var endpoint = request.Endpoint?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || endpoint.Length > 2048
            || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > 200
            || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 100)
        {
            throw new AppException(ErrorKind.Validation, "This browser sent an invalid push subscription.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var hash = Hash(endpoint);
        var existing = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.EndpointHash == hash, cancellationToken);
        if (existing is not null)
        {
            existing.Renew(userId, request.P256dh, request.Auth, now);
        }
        else
        {
            db.PushSubscriptions.Add(new PushSubscription(userId, endpoint, hash, request.P256dh, request.Auth, now));

            // Keep the newest few devices; old phones stop receiving rather than piling up.
            var old = await db.PushSubscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Skip(MaxDevicesPerUser - 1)
                .ToListAsync(cancellationToken);
            db.PushSubscriptions.RemoveRange(old);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Stops push to this browser, e.g. on sign-out. Only the user's own subscription is removed.</summary>
    public async Task UnsubscribeAsync(Guid userId, UnsubscribeRequest request, CancellationToken cancellationToken)
    {
        var hash = Hash(request.Endpoint?.Trim() ?? string.Empty);
        var subscription = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.EndpointHash == hash && s.UserId == userId, cancellationToken);
        if (subscription is not null)
        {
            db.PushSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static string Hash(string endpoint) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
}
