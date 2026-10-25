namespace TowerHaat.Application.Catalog;

public sealed record SaveItemRequest(
    string Name,
    string? Description,
    decimal Price,
    bool? IsVeg,
    int? DailyLimit,
    bool IsAvailableToday,
    bool IsHidden,
    int SortOrder);

public sealed record SetAvailabilityRequest(bool IsAvailableToday);

public sealed record OwnerItemResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    bool? IsVeg,
    int? DailyLimit,
    bool IsAvailableToday,
    bool IsHidden,
    int SortOrder,
    string? PhotoUrl,
    int SoldToday,
    bool IsSoldOut);
