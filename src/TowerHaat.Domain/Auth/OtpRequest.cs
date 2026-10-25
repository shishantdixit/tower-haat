using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Auth;

/// <summary>One OTP sent to a phone. Only a hash of the code is stored.</summary>
public class OtpRequest : Entity
{
    private OtpRequest()
    {
    }

    public OtpRequest(string phone, string codeHash, DateTime createdAt, DateTime expiresAt)
    {
        Phone = phone;
        CodeHash = codeHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string Phone { get; private set; } = null!;

    public string CodeHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public bool IsUsable(DateTime now, int maxAttempts) =>
        ConsumedAt is null && now < ExpiresAt && Attempts < maxAttempts;

    public void RecordFailedAttempt() => Attempts++;

    public void Consume(DateTime now) => ConsumedAt = now;
}
