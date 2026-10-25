namespace TowerHaat.Application.Directory;

public sealed record DirectoryResponse(IReadOnlyList<CategoryChip> Categories, IReadOnlyList<BusinessCard> Businesses);

public sealed record CategoryChip(Guid Id, string Name, int Count);

public sealed record BusinessCard(
    Guid Id,
    string Name,
    string? Description,
    string BusinessTypeName,
    Guid CategoryId,
    string CategoryName,
    bool OrderingEnabled,
    bool IsOpenNow,
    string OpensAt,
    string ClosesAt,
    string? CoverPhotoUrl);

public sealed record BusinessPage(
    Guid Id,
    string Name,
    string? Description,
    string BusinessTypeName,
    string CategoryName,
    bool OrderingEnabled,
    bool IsOpenNow,
    bool IsAcceptingOrders,
    string OpensAt,
    string ClosesAt,
    string? AcceptOrdersUntil,
    decimal MinOrderValue,
    decimal DeliveryCharge,
    string? FssaiNumber,
    string WhatsAppUrl,
    bool IsOwnerPreview,
    IReadOnlyList<string> PhotoUrls,
    IReadOnlyList<MenuItem> Items);

public sealed record MenuItem(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    bool? IsVeg,
    string? PhotoUrl,
    bool IsSoldOut,
    int? RemainingToday);
