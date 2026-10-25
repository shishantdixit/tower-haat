using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Businesses;

/// <summary>Admin-managed category within a business type (A-03), shared by all societies.</summary>
public class Category : Entity
{
    private Category()
    {
    }

    public Category(Guid businessTypeId, string name, int sortOrder)
    {
        BusinessTypeId = businessTypeId;
        Name = name;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public Guid BusinessTypeId { get; private set; }

    public string Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, int sortOrder, bool isActive)
    {
        Name = name;
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}
