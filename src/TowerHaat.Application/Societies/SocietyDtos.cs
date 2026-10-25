namespace TowerHaat.Application.Societies;

public sealed record SocietyResponse(Guid Id, string Name, string City);

public sealed record TowerResponse(Guid Id, string Name, IReadOnlyList<FlatResponse> Flats);

public sealed record FlatResponse(Guid Id, string Number);

public sealed record AdminSocietyResponse(
    Guid Id,
    string Name,
    string City,
    bool IsActive,
    int Towers,
    int Flats,
    int Residents);

public sealed record CreateSocietyRequest(string Name, string City);

public sealed record CreateTowerRequest(string Name);

public sealed record CreateFlatRequest(string Number);

/// <summary>One "Tower,Flat" pair per line; a header line is optional.</summary>
public sealed record ImportFlatsRequest(string Csv);

public sealed record ImportFlatsResponse(
    int TowersCreated,
    int FlatsCreated,
    int FlatsAlreadyListed,
    IReadOnlyList<ImportError> Errors);

public sealed record ImportError(int Line, string Message);
