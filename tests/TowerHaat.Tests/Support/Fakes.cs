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
