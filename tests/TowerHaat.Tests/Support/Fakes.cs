using TowerHaat.Application.Abstractions;

namespace TowerHaat.Tests.Support;

public sealed class CapturingSmsSender : ISmsSender
{
    private readonly List<(string Phone, string Code)> _sent = [];

    public IReadOnlyList<(string Phone, string Code)> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public string LastCodeFor(string phone) => Sent.Last(s => s.Phone == phone).Code;

    public Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken)
    {
        lock (_sent)
        {
            _sent.Add((phone, code));
        }

        return Task.CompletedTask;
    }
}

public sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessTokenClaims? Last { get; private set; }

    public AccessToken Issue(AccessTokenClaims claims)
    {
        Last = claims;
        return new AccessToken($"token-for-{claims.UserId}", DateTime.UtcNow.AddMinutes(30));
    }
}

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public IReadOnlyCollection<string> Keys => _files.Keys;

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var key = $"2026/10/{Guid.NewGuid():N}{extension}";
        _files[key] = copy.ToArray();
        return key;
    }

    public Stream? OpenRead(string key) => _files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null;

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _files.Remove(key);
        return Task.CompletedTask;
    }
}

public static class TestImages
{
    /// <summary>The first bytes of a JPEG; enough for the upload checks.</summary>
    public static MemoryStream Jpeg() => new([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01]);

    public static MemoryStream NotAnImage() => new("<html>not an image</html>"u8.ToArray());
}
