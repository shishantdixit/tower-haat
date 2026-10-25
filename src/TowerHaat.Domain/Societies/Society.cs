using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Societies;

/// <summary>The tenant itself, so it is not an <see cref="ITenantEntity"/>.</summary>
public class Society : Entity
{
    private Society()
    {
    }

    public Society(string name, string city)
    {
        Name = name;
        City = city;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;

    public string City { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
