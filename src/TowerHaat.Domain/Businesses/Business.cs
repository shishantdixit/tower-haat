using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Businesses;

/// <summary>
/// A home business run by a resident (B-01). It is Pending until a platform admin approves it
/// (A-02); only Active businesses appear in the society's directory.
/// </summary>
public class Business : Entity, ITenantEntity
{
    public const int MaxPhotos = 6;

    private readonly List<BusinessPhoto> _photos = [];

    private Business()
    {
    }

    public Business(Guid ownerUserId, BusinessType type, Category category, BusinessProfile profile, DateTime createdAt)
    {
        if (category.BusinessTypeId != type.Id)
        {
            throw new InvalidOperationException("The category belongs to another business type.");
        }

        OwnerUserId = ownerUserId;
        BusinessTypeId = type.Id;
        CategoryId = category.Id;
        Status = BusinessStatus.Pending;
        CreatedAt = createdAt;
        ApplyProfile(profile);
    }

    public Guid SocietyId { get; set; }

    public Guid OwnerUserId { get; private set; }

    public Guid BusinessTypeId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? UpiId { get; private set; }

    public string? FssaiNumber { get; private set; }

    /// <summary>Number customers reach on WhatsApp (C-03); defaults to the owner's phone.</summary>
    public string WhatsAppPhone { get; private set; } = null!;

    public BusinessStatus Status { get; private set; }

    /// <summary>The admin's note on the last approval decision, shown to the owner.</summary>
    public string? StatusNote { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public BusinessHours Hours { get; private set; } = BusinessHours.Default;

    /// <summary>The owner's open/closed switch (B-02). Closed overrides the hours.</summary>
    public bool IsOpen { get; private set; } = true;

    public decimal MinOrderValue { get; private set; }

    public decimal DeliveryCharge { get; private set; }

    public IReadOnlyCollection<BusinessPhoto> Photos => _photos;

    /// <summary>Edits the profile. A rejected business goes back to Pending for another review.</summary>
    public void UpdateProfile(Category category, BusinessProfile profile)
    {
        if (category.BusinessTypeId != BusinessTypeId)
        {
            throw new InvalidOperationException("The category belongs to another business type.");
        }

        CategoryId = category.Id;
        ApplyProfile(profile);

        if (Status == BusinessStatus.Rejected)
        {
            Status = BusinessStatus.Pending;
        }
    }

    public void UpdateSettings(BusinessHours hours, decimal minOrderValue, decimal deliveryCharge)
    {
        if (minOrderValue < 0 || deliveryCharge < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minOrderValue), "Amounts cannot be negative.");
        }

        Hours = hours;
        MinOrderValue = minOrderValue;
        DeliveryCharge = deliveryCharge;
    }

    public void SetOpen(bool isOpen) => IsOpen = isOpen;

    public void Approve(string? note, DateTime now)
    {
        RequireStatus(BusinessStatus.Pending, BusinessStatus.Rejected, BusinessStatus.Deactivated);
        Status = BusinessStatus.Active;
        StatusNote = note;
        ApprovedAt ??= now;
    }

    public void Reject(string note)
    {
        RequireStatus(BusinessStatus.Pending);
        Status = BusinessStatus.Rejected;
        StatusNote = note;
    }

    public void Deactivate(string note)
    {
        RequireStatus(BusinessStatus.Active, BusinessStatus.Pending);
        Status = BusinessStatus.Deactivated;
        StatusNote = note;
    }

    /// <summary>Open for customers right now: active, switched on and within today's hours (C-02).</summary>
    public bool IsOpenAt(TimeOnly localTime) => Status == BusinessStatus.Active && IsOpen && Hours.Contains(localTime);

    public BusinessPhoto AddPhoto(string fileKey)
    {
        if (_photos.Count >= MaxPhotos)
        {
            throw new InvalidOperationException($"A business can have at most {MaxPhotos} photos.");
        }

        var photo = new BusinessPhoto(this, fileKey, _photos.Count == 0 ? 0 : _photos.Max(p => p.SortOrder) + 1);
        _photos.Add(photo);
        return photo;
    }

    public void RemovePhoto(BusinessPhoto photo) => _photos.Remove(photo);

    private void ApplyProfile(BusinessProfile profile)
    {
        Name = profile.Name;
        Description = profile.Description;
        UpiId = profile.UpiId;
        FssaiNumber = profile.FssaiNumber;
        WhatsAppPhone = profile.WhatsAppPhone;
    }

    private void RequireStatus(params BusinessStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"A {Status} business cannot change this way.");
        }
    }
}

public sealed record BusinessProfile(
    string Name,
    string? Description,
    string? UpiId,
    string? FssaiNumber,
    string WhatsAppPhone);
