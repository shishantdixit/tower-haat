using TowerHaat.Domain.Businesses;

namespace TowerHaat.Application.Businesses;

public sealed record AdminBusinessResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid SocietyId,
    string SocietyName,
    string OwnerName,
    string OwnerPhone,
    string OwnerAddress,
    string BusinessTypeName,
    string CategoryName,
    string? UpiId,
    string? FssaiNumber,
    BusinessStatus Status,
    string? StatusNote,
    DateTime CreatedAt,
    IReadOnlyList<string> PhotoUrls);

public sealed record BusinessDecisionRequest(string? Note);
