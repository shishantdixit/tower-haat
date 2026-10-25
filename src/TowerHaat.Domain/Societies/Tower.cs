using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Societies;

public class Tower : Entity, ITenantEntity
{
    private readonly List<Flat> _flats = [];

    private Tower()
    {
    }

    public Tower(string name)
    {
        Name = name;
    }

    public Guid SocietyId { get; set; }

    public string Name { get; private set; } = null!;

    public IReadOnlyCollection<Flat> Flats => _flats;
}
