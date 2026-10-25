using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Notifications;

namespace TowerHaat.Infrastructure.Push;

/// <summary>
/// Web push with VAPID and aes128gcm encryption (RFC 8291/8292) through Lib.Net.Http.WebPush.
/// Works with Chrome/Android (FCM), Firefox and Safari/iOS 16.4+ (installed PWA).
/// Without VAPID keys configured it sends nothing.
/// </summary>
internal sealed partial class WebPushSender : IPushSender
{
    private readonly PushServiceClient? _client;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(HttpClient http, IOptions<PushOptions> options, ILogger<WebPushSender> logger)
    {
        _logger = logger;
        var push = options.Value;
        if (!push.IsConfigured)
        {
            return;
        }

        _client = new PushServiceClient(http)
        {
            DefaultAuthentication = new VapidAuthentication(push.PublicKey, push.PrivateKey) { Subject = push.Subject },
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
            DefaultTimeToLive = (int)TimeSpan.FromMinutes(30).TotalSeconds,
        };
    }

    public bool IsConfigured => _client is not null;

    public async Task<PushResult> SendAsync(PushTarget target, string payloadJson, string? topic, CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return PushResult.Failed;
        }

        var subscription = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = target.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, target.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, target.Auth);

        var message = new PushMessage(payloadJson) { Topic = topic, Urgency = PushMessageUrgency.High };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
            return PushResult.Sent;
        }
        catch (PushServiceClientException e) when (e.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushResult.Gone;
        }
        catch (Exception e) when (e is PushServiceClientException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogFailed(new Uri(target.Endpoint).Host, e.Message);
            return PushResult.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web push to {Host} failed: {Reason}")]
    private partial void LogFailed(string host, string reason);
}
