using TowerHaat.Application.Residents;

namespace TowerHaat.Application.Auth;

public sealed record RequestOtpRequest(string Phone);

public sealed record OtpRequestedResponse(int ExpiresInSeconds, int ResendAfterSeconds, string? DevelopmentCode);

public sealed record VerifyOtpRequest(string Phone, string Code);

/// <summary>The refresh token travels in an HTTP-only cookie, never in the body.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    MeResponse User);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, MeResponse User);
