using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Businesses;

public class BusinessPhoto : Entity, ITenantEntity
{
    private BusinessPhoto()
    {
    }

    internal BusinessPhoto(Business business, string fileKey, int sortOrder)
    {
        SocietyId = business.SocietyId;
        BusinessId = business.Id;
        FileKey = fileKey;
        SortOrder = sortOrder;
    }

    public Guid SocietyId { get; set; }

    public Guid BusinessId { get; private set; }

    /// <summary>Key in <c>IFileStorage</c>.</summary>
    public string FileKey { get; private set; } = null!;

    public int SortOrder { get; private set; }
}
