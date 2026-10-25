using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Catalog;

/// <summary>An item a kitchen or seller offers (B-03).</summary>
public class CatalogItem : Entity, ITenantEntity
{
    private CatalogItem()
    {
    }

    public CatalogItem(Business business, CatalogItemDetails details, DateTime createdAt)
    {
        SocietyId = business.SocietyId;
        BusinessId = business.Id;
        CreatedAt = createdAt;
        IsAvailableToday = true;
        Update(details);
    }

    public Guid SocietyId { get; set; }

    public Guid BusinessId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    /// <summary>True veg, false non-veg, null when it does not apply (products).</summary>
    public bool? IsVeg { get; private set; }

    /// <summary>Most that can be sold in a day; null for no limit. Resets at midnight.</summary>
    public int? DailyLimit { get; private set; }

    public bool IsAvailableToday { get; private set; }

    /// <summary>Hidden items stay in the owner's catalog but customers never see them.</summary>
    public bool IsHidden { get; private set; }

    public string? PhotoKey { get; private set; }

    public int SortOrder { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Update(CatalogItemDetails details)
    {
        if (details.Price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "Price must be more than zero.");
        }

        if (details.DailyLimit is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "Daily limit cannot be negative.");
        }

        Name = details.Name;
        Description = details.Description;
        Price = details.Price;
        IsVeg = details.IsVeg;
        DailyLimit = details.DailyLimit;
        IsHidden = details.IsHidden;
        SortOrder = details.SortOrder;
    }

    public void SetAvailableToday(bool available) => IsAvailableToday = available;

    public void SetPhoto(string? photoKey) => PhotoKey = photoKey;

    /// <summary>Sold out when switched off for today or the daily limit is reached (C-04).</summary>
    public bool IsSoldOut(int soldToday) => !IsAvailableToday || (DailyLimit is { } limit && soldToday >= limit);

    public int? RemainingToday(int soldToday) => DailyLimit is { } limit ? Math.Max(0, limit - soldToday) : null;
}

public sealed record CatalogItemDetails(
    string Name,
    string? Description,
    decimal Price,
    bool? IsVeg,
    int? DailyLimit,
    bool IsHidden,
    int SortOrder);
