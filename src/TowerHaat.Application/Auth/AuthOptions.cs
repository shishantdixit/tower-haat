namespace TowerHaat.Application.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(60);

    /// <summary>Phones (+91…) that become platform admins when they sign in.</summary>
    public List<string> AdminPhones { get; set; } = [];

    public OtpOptions Otp { get; set; } = new();
}

public sealed class OtpOptions
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRequestsPerHour { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    /// <summary>Secret for hashing codes. Required.</summary>
    public string HashKey { get; set; } = string.Empty;

    /// <summary>Returns the code in the API response. Honoured in Development only, until SMS is live.</summary>
    public bool ExposeCodeInResponse { get; set; }
}
