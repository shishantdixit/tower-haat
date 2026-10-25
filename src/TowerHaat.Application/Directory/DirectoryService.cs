using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Common;
using TowerHaat.Application.Media;
using TowerHaat.Domain.Businesses;

namespace TowerHaat.Application.Directory;

/// <summary>What residents browse: the society's businesses (C-02) and a business page with its menu (C-03, C-04).</summary>
public sealed class DirectoryService(IAppDbContext db, ICurrentSociety society, TimeProvider time)
{
    public async Task<DirectoryResponse> ListAsync(CancellationToken cancellationToken)
    {
        RequireSociety();
        var now = IndiaTime.TimeNow(time);

        var businesses = await db.Businesses
            .Include(b => b.Photos)
            .Where(b => b.Status == BusinessStatus.Active)
            .ToListAsync(cancellationToken);
        var types = await db.BusinessTypes.ToDictionaryAsync(t => t.Id, cancellationToken);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, cancellationToken);

        var cards = businesses
            .Select(b =>
            {
                var settings = OwnerBusinessService.Settings(b);
                return new BusinessCard(
                    b.Id,
                    b.Name,
                    b.Description,
                    types[b.BusinessTypeId].Name,
                    b.CategoryId,
                    categories[b.CategoryId].Name,
                    types[b.BusinessTypeId].OrderingEnabled,
                    b.IsOpenAt(now),
                    settings.OpensAt,
                    settings.ClosesAt,
                    ImageUpload.Url(b.Photos.OrderBy(p => p.SortOrder).FirstOrDefault()?.FileKey));
            })
            .OrderByDescending(c => c.IsOpenNow)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chips = cards
            .GroupBy(c => c.CategoryId)
            .Select(g => (Category: categories[g.Key], Count: g.Count()))
            .OrderBy(x => types[x.Category.BusinessTypeId].SortOrder)
            .ThenBy(x => x.Category.SortOrder)
            .ThenBy(x => x.Category.Name)
            .Select(x => new CategoryChip(x.Category.Id, x.Category.Name, x.Count))
            .ToList();

        return new DirectoryResponse(chips, cards);
    }

    /// <summary>An active business of the resident's society; owners can also preview their own before approval.</summary>
    public async Task<BusinessPage> GetAsync(Guid userId, Guid businessId, CancellationToken cancellationToken)
    {
        RequireSociety();

        var business = await db.Businesses
            .Include(b => b.Photos)
            .SingleOrDefaultAsync(b => b.Id == businessId, cancellationToken);
        var isOwner = business?.OwnerUserId == userId;
        if (business is null || (business.Status != BusinessStatus.Active && !isOwner))
        {
            throw new AppException(ErrorKind.NotFound, "This business is not available.");
        }

        var type = await db.BusinessTypes.SingleAsync(t => t.Id == business.BusinessTypeId, cancellationToken);
        var category = await db.Categories.SingleAsync(c => c.Id == business.CategoryId, cancellationToken);
        var items = type.OrderingEnabled
            ? await db.CatalogItems.Where(i => i.BusinessId == business.Id && !i.IsHidden).ToListAsync(cancellationToken)
            : [];
        var sold = await SoldToday.ForBusinessAsync(db, business.Id, time, cancellationToken);

        var now = IndiaTime.TimeNow(time);
        var settings = OwnerBusinessService.Settings(business);
        var isOpen = business.IsOpenAt(now);

        return new BusinessPage(
            business.Id,
            business.Name,
            business.Description,
            type.Name,
            category.Name,
            type.OrderingEnabled,
            isOpen,
            type.OrderingEnabled && isOpen && business.Hours.AcceptsOrdersAt(now),
            settings.OpensAt,
            settings.ClosesAt,
            settings.AcceptOrdersUntil,
            business.MinOrderValue,
            business.DeliveryCharge,
            business.FssaiNumber,
            business.UpiId is not null,
            $"https://wa.me/{business.WhatsAppPhone.TrimStart('+')}",
            isOwner && business.Status != BusinessStatus.Active,
            business.Photos.OrderBy(p => p.SortOrder).Select(p => ImageUpload.Url(p.FileKey)!).ToList(),
            items
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                .Select(i =>
                {
                    var soldToday = sold.GetValueOrDefault(i.Id);
                    return new MenuItem(
                        i.Id,
                        i.Name,
                        i.Description,
                        i.Price,
                        i.IsVeg,
                        ImageUpload.Url(i.PhotoKey),
                        i.IsSoldOut(soldToday),
                        i.RemainingToday(soldToday));
                })
                .ToList());
    }

    private void RequireSociety()
    {
        if (society.SocietyId is null)
        {
            throw new AppException(ErrorKind.Forbidden, "Finish signing up to see your society's businesses.");
        }
    }
}
