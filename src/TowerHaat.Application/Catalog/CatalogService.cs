using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Common;
using TowerHaat.Application.Media;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;

namespace TowerHaat.Application.Catalog;

/// <summary>The owner's catalog (B-03): add, edit, hide items, photo, veg tag, daily limit, available today.</summary>
public sealed class CatalogService(IAppDbContext db, OwnerBusinessService owners, IFileStorage files, TimeProvider time)
{
    public const int MaxItems = 200;
    public const decimal MaxPrice = 100_000;
    public const int MaxDailyLimit = 10_000;

    public async Task<List<OwnerItemResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var business = await FindOrderingBusinessAsync(userId, cancellationToken);
        var items = await db.CatalogItems.Where(i => i.BusinessId == business.Id).ToListAsync(cancellationToken);
        var sold = await SoldToday.ForBusinessAsync(db, business.Id, time, cancellationToken);

        return items
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => ToResponse(i, sold.GetValueOrDefault(i.Id)))
            .ToList();
    }

    public async Task<OwnerItemResponse> CreateAsync(Guid userId, SaveItemRequest request, CancellationToken cancellationToken)
    {
        var business = await FindOrderingBusinessAsync(userId, cancellationToken);
        if (await db.CatalogItems.CountAsync(i => i.BusinessId == business.Id, cancellationToken) >= MaxItems)
        {
            throw new AppException(ErrorKind.Validation, $"A catalog can have up to {MaxItems} items.");
        }

        var item = new CatalogItem(business, ToDetails(request), time.GetUtcNow().UtcDateTime);
        item.SetAvailableToday(request.IsAvailableToday);
        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(item, soldToday: 0);
    }

    public async Task<OwnerItemResponse> UpdateAsync(Guid userId, Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(userId, itemId, cancellationToken);
        item.Update(ToDetails(request));
        item.SetAvailableToday(request.IsAvailableToday);
        await db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(item, cancellationToken);
    }

    /// <summary>One-tap "available today" switch for busy owners.</summary>
    public async Task<OwnerItemResponse> SetAvailabilityAsync(Guid userId, Guid itemId, bool available, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(userId, itemId, cancellationToken);
        item.SetAvailableToday(available);
        await db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(item, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(userId, itemId, cancellationToken);
        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        if (item.PhotoKey is { } key)
        {
            await files.DeleteAsync(key, cancellationToken);
        }
    }

    public async Task<OwnerItemResponse> SetPhotoAsync(Guid userId, Guid itemId, Stream content, long length, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(userId, itemId, cancellationToken);
        var (image, extension) = await ImageUpload.ReadAsync(content, length, cancellationToken);

        string key;
        await using (image)
        {
            key = await files.SaveAsync(image, extension, cancellationToken);
        }

        var previous = item.PhotoKey;
        item.SetPhoto(key);
        await db.SaveChangesAsync(cancellationToken);

        if (previous is not null)
        {
            await files.DeleteAsync(previous, cancellationToken);
        }

        return await ToResponseAsync(item, cancellationToken);
    }

    public async Task<OwnerItemResponse> RemovePhotoAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await FindItemAsync(userId, itemId, cancellationToken);
        var previous = item.PhotoKey;
        item.SetPhoto(null);
        await db.SaveChangesAsync(cancellationToken);

        if (previous is not null)
        {
            await files.DeleteAsync(previous, cancellationToken);
        }

        return await ToResponseAsync(item, cancellationToken);
    }

    private async Task<Business> FindOrderingBusinessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var business = await owners.FindEditableAsync(userId, cancellationToken);
        var orderingEnabled = await db.BusinessTypes
            .Where(t => t.Id == business.BusinessTypeId)
            .Select(t => t.OrderingEnabled)
            .SingleAsync(cancellationToken);

        return orderingEnabled
            ? business
            : throw new AppException(ErrorKind.Validation, "This kind of business takes enquiries, so it has no catalog.");
    }

    private async Task<CatalogItem> FindItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var business = await FindOrderingBusinessAsync(userId, cancellationToken);
        return await db.CatalogItems.SingleOrDefaultAsync(i => i.Id == itemId && i.BusinessId == business.Id, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Item not found.");
    }

    private static CatalogItemDetails ToDetails(SaveItemRequest request)
    {
        if (request.Price <= 0 || request.Price > MaxPrice || decimal.Round(request.Price, 2) != request.Price)
        {
            throw new AppException(ErrorKind.Validation, $"Price must be between ₹1 and ₹{MaxPrice:0} with at most 2 decimals.");
        }

        if (request.DailyLimit is < 0 or > MaxDailyLimit)
        {
            throw new AppException(ErrorKind.Validation, $"Daily limit must be between 0 and {MaxDailyLimit}.");
        }

        return new CatalogItemDetails(
            Validate.Required(request.Name, "Item name", 100),
            Validate.Optional(request.Description, "Description", 500),
            request.Price,
            request.IsVeg,
            request.DailyLimit,
            request.IsHidden,
            request.SortOrder);
    }

    private async Task<OwnerItemResponse> ToResponseAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        var sold = await SoldToday.ForBusinessAsync(db, item.BusinessId, time, cancellationToken);
        return ToResponse(item, sold.GetValueOrDefault(item.Id));
    }

    private static OwnerItemResponse ToResponse(CatalogItem item, int soldToday) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Price,
        item.IsVeg,
        item.DailyLimit,
        item.IsAvailableToday,
        item.IsHidden,
        item.SortOrder,
        ImageUpload.Url(item.PhotoKey),
        soldToday,
        item.IsSoldOut(soldToday));
}
