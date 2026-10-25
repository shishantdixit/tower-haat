using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Societies;

public class Tower : Entity, ITenantEntity
{
    private Tower()
    {
    }

    public Tower(string name)
    {
        Name = name;
    }

    public Guid SocietyId { get; set; }

    public string Name { get; private set; } = null!;
}
