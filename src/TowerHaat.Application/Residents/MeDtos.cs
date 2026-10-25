namespace TowerHaat.Application.Residents;

public sealed record MeResponse(Guid Id, string Phone, string? Name, bool IsPlatformAdmin, ProfileResponse? Profile);

public sealed record ProfileResponse(
    Guid SocietyId,
    string SocietyName,
    Guid? FlatId,
    string Tower,
    string Flat,
    bool IsListedFlat);

/// <summary>Either <see cref="FlatId"/> from the list, or both manual fields when the flat is missing from it.</summary>
public sealed record SaveProfileRequest(
    string Name,
    Guid SocietyId,
    Guid? FlatId,
    string? ManualTower,
    string? ManualFlat,
    bool Consent);

public sealed record SaveProfileResponse(string AccessToken, DateTime AccessTokenExpiresAt, MeResponse User);
