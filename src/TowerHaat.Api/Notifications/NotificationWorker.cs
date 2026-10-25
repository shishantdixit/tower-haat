using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Notifications;

namespace TowerHaat.Api.Notifications;

/// <summary>Delivers queued notifications: a live event to open apps, then web push to every device of the user.</summary>
internal sealed partial class NotificationWorker(
    ChannelNotificationQueue queue,
    IHubContext<OrdersHub> hub,
    IServiceScopeFactory scopes,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(notification, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(e);
            }
        }
    }

    internal async Task DeliverAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Event is { } orderChanged)
        {
            await hub.Clients.User(notification.UserId.ToString()).SendAsync(OrdersHub.OrderChanged, orderChanged, cancellationToken);
        }

        using var scope = scopes.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IPushSender>();
        if (!sender.IsConfigured)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var subscriptions = await db.PushSubscriptions.Where(s => s.UserId == notification.UserId).ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = Payload(notification);
        var topic = notification.Event?.OrderId.ToString("N");
        var gone = false;
        foreach (var subscription in subscriptions)
        {
            var result = await sender.SendAsync(new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, topic, cancellationToken);
            if (result == PushResult.Gone)
            {
                db.PushSubscriptions.Remove(subscription);
                gone = true;
            }
        }

        if (gone)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>The shape Angular's service worker shows as a notification, opening the URL when tapped.</summary>
    internal static string Payload(UserNotification n) => JsonSerializer.Serialize(new
    {
        notification = new
        {
            title = n.Title,
            body = n.Body,
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = n.Tag,
            renotify = true,
            data = new
            {
                url = n.Url,
                onActionClick = new { @default = new { operation = "navigateLastFocusedOrOpen", url = n.Url } },
            },
        },
    }, Json);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivering a notification failed")]
    private partial void LogFailed(Exception exception);
}
