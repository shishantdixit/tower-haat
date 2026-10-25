using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Businesses;

/// <summary>
/// Admin-managed kind of business (A-03), shared by all societies. Types with ordering get a
/// catalog and orders; the rest get a profile, listings and enquiries.
/// </summary>
public class BusinessType : Entity
{
    private BusinessType()
    {
    }

    public BusinessType(string name, bool orderingEnabled, int sortOrder)
    {
        Name = name;
        OrderingEnabled = orderingEnabled;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;

    public bool OrderingEnabled { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, bool orderingEnabled, int sortOrder, bool isActive)
    {
        Name = name;
        OrderingEnabled = orderingEnabled;
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}
