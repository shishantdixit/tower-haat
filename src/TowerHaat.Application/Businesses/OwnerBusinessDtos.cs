using TowerHaat.Domain.Businesses;

namespace TowerHaat.Application.Businesses;

/// <summary>Registration and profile edits (B-01). The type is fixed once registered.</summary>
public sealed record SaveBusinessRequest(
    Guid BusinessTypeId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? UpiId,
    string? FssaiNumber,
    string? WhatsAppPhone);

/// <summary>Times as "HH:mm" in Indian time (B-02).</summary>
public sealed record BusinessSettingsRequest(
    string OpensAt,
    string ClosesAt,
    string? AcceptOrdersUntil,
    decimal MinOrderValue,
    decimal DeliveryCharge);

public sealed record SetOpenRequest(bool IsOpen);

public sealed record OwnerBusinessResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid BusinessTypeId,
    string BusinessTypeName,
    bool OrderingEnabled,
    Guid CategoryId,
    string CategoryName,
    string? UpiId,
    string? FssaiNumber,
    string WhatsAppPhone,
    BusinessStatus Status,
    string? StatusNote,
    BusinessSettingsResponse Settings,
    bool IsOpenNow,
    IReadOnlyList<PhotoResponse> Photos);

public sealed record BusinessSettingsResponse(
    string OpensAt,
    string ClosesAt,
    string? AcceptOrdersUntil,
    bool IsOpen,
    decimal MinOrderValue,
    decimal DeliveryCharge);

public sealed record PhotoResponse(Guid Id, string Url);
