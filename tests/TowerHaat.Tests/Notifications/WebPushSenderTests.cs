using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Notifications;

namespace TowerHaat.Tests.Notifications;

/// <summary>
/// The real sender against a fake push service. The test plays the browser: it decrypts the body
/// with its own keys (RFC 8291, aes128gcm), proving the message would be readable on a phone.
/// </summary>
public sealed class WebPushSenderTests
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc123";
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Message_is_signed_and_encrypted_so_only_the_browser_can_read_it()
    {
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var authSecret = RandomNumberGenerator.GetBytes(16);
        var handler = new FakePushService(HttpStatusCode.Created);
        var sender = CreateSender(handler);

        var result = await sender.SendAsync(
            new PushTarget(Endpoint, Base64Url(PublicKey(browser)), Base64Url(authSecret)), """{"notification":{"title":"New order #1"}}""", "a1b2c3", _ct);

        Assert.Equal(PushResult.Sent, result);
        var request = handler.Request!;
        Assert.Equal(Endpoint, request.Uri.ToString());
        Assert.StartsWith("vapid t=", request.Authorization);
        Assert.Contains("k=", request.Authorization);
        Assert.Equal("aes128gcm", request.ContentEncoding);
        Assert.Equal("1800", request.Ttl);
        Assert.Equal("high", request.Urgency);
        Assert.Equal("a1b2c3", request.Topic);
        Assert.Equal("""{"notification":{"title":"New order #1"}}""", Decrypt(request.Body, browser, authSecret));
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, PushResult.Gone)]
    [InlineData(HttpStatusCode.NotFound, PushResult.Gone)]
    [InlineData(HttpStatusCode.BadRequest, PushResult.Failed)]
    public async Task Push_service_answers_are_mapped(HttpStatusCode status, PushResult expected)
    {
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var sender = CreateSender(new FakePushService(status));

        var result = await sender.SendAsync(
            new PushTarget(Endpoint, Base64Url(PublicKey(browser)), Base64Url(RandomNumberGenerator.GetBytes(16))), "{}", null, _ct);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Without_keys_nothing_is_sent()
    {
        var handler = new FakePushService(HttpStatusCode.Created);
        var sender = CreateSender(handler, configured: false);

        Assert.False(sender.IsConfigured);
        Assert.Equal(PushResult.Failed, await sender.SendAsync(new PushTarget(Endpoint, "x", "y"), "{}", null, _ct));
        Assert.Null(handler.Request);
    }

    private static IPushSender CreateSender(FakePushService handler, bool configured = true)
    {
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = vapid.ExportParameters(true);
        var options = configured
            ? new PushOptions { PublicKey = Base64Url([0x04, .. p.Q.X!, .. p.Q.Y!]), PrivateKey = Base64Url(p.D!), Subject = "mailto:test@towerhaat.com" }
            : new PushOptions();

        var type = typeof(TowerHaat.Infrastructure.DependencyInjection).Assembly.GetType("TowerHaat.Infrastructure.Push.WebPushSender")!;
        var loggerType = typeof(NullLogger<>).MakeGenericType(type);
        var logger = loggerType.GetField("Instance")!.GetValue(null)!;
        return (IPushSender)Activator.CreateInstance(type, new HttpClient(handler), Options.Create(options), logger)!;
    }

    private static byte[] PublicKey(ECDiffieHellman key)
    {
        var p = key.ExportParameters(false);
        return [0x04, .. p.Q.X!, .. p.Q.Y!];
    }

    /// <summary>RFC 8291 decryption, as a browser does it.</summary>
    private static string Decrypt(byte[] body, ECDiffieHellman browser, byte[] authSecret)
    {
        var salt = body[..16];
        var keyIdLength = body[20];
        var serverPublic = body[21..(21 + keyIdLength)];
        var cipher = body[(21 + keyIdLength)..];

        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
        });
        var shared = browser.DeriveRawSecretAgreement(server.PublicKey);

        byte[] keyInfo = [.. "WebPush: info\0"u8, .. PublicKey(browser), .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        var plain = new byte[cipher.Length - 16];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);

        // Strip the padding delimiter (0x02 for the last record) and any zero padding after it.
        var end = Array.LastIndexOf(plain, (byte)0x02);
        return Encoding.UTF8.GetString(plain, 0, end);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakePushService(HttpStatusCode status) : HttpMessageHandler
    {
        public CapturedRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = new CapturedRequest(
                request.RequestUri!,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                string.Join(",", request.Content!.Headers.ContentEncoding),
                request.Headers.TryGetValues("TTL", out var ttl) ? ttl.Single() : null,
                request.Headers.TryGetValues("Urgency", out var urgency) ? urgency.Single() : null,
                request.Headers.TryGetValues("Topic", out var topic) ? topic.Single() : null,
                await request.Content.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(status);
        }
    }

    private sealed record CapturedRequest(Uri Uri, string Authorization, string ContentEncoding, string? Ttl, string? Urgency, string? Topic, byte[] Body);
}
