namespace TowerHaat.Application.Notifications;

/// <summary>
/// Something a user should hear about now: sent as web push to their devices and as a live event
/// to any open app. Built completely when queued, so delivery needs no database or society scope.
/// </summary>
public sealed record UserNotification(
    Guid UserId,
    string Title,
    string Body,
    string Url,
    string Tag,
    OrderChangedEvent? Event);

/// <summary>Live event that tells an open app to refresh an order (M5 replaces polling).</summary>
public sealed record OrderChangedEvent(Guid OrderId, Guid BusinessId, string Status, string PaymentStatus, bool IsNewOrder);

/// <summary>Hands notifications to a background sender, so requests never wait on push services.</summary>
public interface INotificationQueue
{
    void Enqueue(UserNotification notification);
}
