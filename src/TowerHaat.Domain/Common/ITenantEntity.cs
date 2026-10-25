namespace TowerHaat.Domain.Common;

/// <summary>
/// Marks an entity owned by one society. Every society-owned entity implements this,
/// including child entities (items, payments, history rows), so the tenancy query filter
/// covers them directly instead of relying on a parent being filtered.
/// </summary>
public interface ITenantEntity
{
    Guid SocietyId { get; set; }
}
