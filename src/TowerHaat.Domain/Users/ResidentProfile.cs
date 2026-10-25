using TowerHaat.Domain.Common;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Domain.Users;

/// <summary>
/// Where a user lives. The flat is picked from the society's list; manual tower and flat
/// text is used only when the flat is missing from it (C-01).
/// </summary>
public class ResidentProfile : Entity, ITenantEntity
{
    private ResidentProfile()
    {
    }

    public ResidentProfile(Guid userId, Guid societyId, DateTime consentedAt)
    {
        UserId = userId;
        SocietyId = societyId;
        ConsentedAt = consentedAt;
    }

    public Guid SocietyId { get; set; }

    public Guid UserId { get; private set; }

    public Guid? FlatId { get; private set; }

    public Flat? Flat { get; private set; }

    public string? ManualTower { get; private set; }

    public string? ManualFlat { get; private set; }

    /// <summary>When the resident agreed to the use of phone, name, tower and flat for orders (DPDP Act).</summary>
    public DateTime ConsentedAt { get; private set; }

    public void LiveIn(Flat flat)
    {
        if (flat.SocietyId != SocietyId)
        {
            throw new InvalidOperationException("The flat belongs to another society.");
        }

        FlatId = flat.Id;
        Flat = flat;
        ManualTower = null;
        ManualFlat = null;
    }

    public void LiveInUnlistedFlat(string tower, string flat)
    {
        FlatId = null;
        Flat = null;
        ManualTower = tower;
        ManualFlat = flat;
    }
}
