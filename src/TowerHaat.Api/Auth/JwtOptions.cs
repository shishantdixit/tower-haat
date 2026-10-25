namespace TowerHaat.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "towerhaat";

    public string Audience { get; set; } = "towerhaat";

    /// <summary>At least 32 characters. Set per environment, never committed for production.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
}
