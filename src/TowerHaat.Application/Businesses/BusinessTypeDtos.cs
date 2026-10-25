namespace TowerHaat.Application.Businesses;

public sealed record BusinessTypeResponse(
    Guid Id,
    string Name,
    bool OrderingEnabled,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<CategoryResponse> Categories);

public sealed record CategoryResponse(Guid Id, string Name, int SortOrder, bool IsActive);

public sealed record SaveBusinessTypeRequest(string Name, bool OrderingEnabled, int SortOrder, bool IsActive);

public sealed record SaveCategoryRequest(string Name, int SortOrder, bool IsActive);
