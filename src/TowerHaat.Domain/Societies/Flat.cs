using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Societies;

public class Flat : Entity, ITenantEntity
{
    private Flat()
    {
    }

    public Flat(Tower tower, string number)
    {
        SocietyId = tower.SocietyId;
        TowerId = tower.Id;
        Tower = tower;
        Number = number;
    }

    public Guid SocietyId { get; set; }

    public Guid TowerId { get; private set; }

    public Tower Tower { get; private set; } = null!;

    public string Number { get; private set; } = null!;
}
