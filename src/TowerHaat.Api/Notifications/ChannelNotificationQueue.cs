using System.Threading.Channels;
using TowerHaat.Application.Notifications;

namespace TowerHaat.Api.Notifications;

/// <summary>
/// In-memory queue drained by <see cref="NotificationWorker"/>. Bounded, dropping the oldest when
/// full, so a push outage can never slow down orders. Lost on restart, which only means a missed
/// alert: the order itself is safe in the database and the apps still refresh.
/// </summary>
internal sealed class ChannelNotificationQueue : INotificationQueue
{
    private readonly Channel<UserNotification> _channel = Channel.CreateBounded<UserNotification>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<UserNotification> Reader => _channel.Reader;

    public void Enqueue(UserNotification notification) => _channel.Writer.TryWrite(notification);
}
