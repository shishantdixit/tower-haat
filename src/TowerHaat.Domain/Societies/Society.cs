using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Societies;

/// <summary>The tenant itself, so it is not an <see cref="ITenantEntity"/>.</summary>
public class Society : Entity
{
    /// <summary>The launch society, seeded by the AddResidents migration.</summary>
    public static readonly Guid AceDivinoId = new("01920000-0000-7000-8000-000000000001");

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
