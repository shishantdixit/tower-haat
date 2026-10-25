using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Media;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Users;

namespace TowerHaat.Application.Businesses;

/// <summary>A resident's own business: registration (B-01), profile, settings (B-02) and photos.</summary>
public sealed class OwnerBusinessService(IAppDbContext db, ICurrentSociety society, IFileStorage files, TimeProvider time)
{
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 1000;
    public const decimal MaxAmount = 10_000;

    public async Task<OwnerBusinessResponse?> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var business = await FindMineAsync(userId, cancellationToken);
        return business is null ? null : await ToResponseAsync(business, cancellationToken);
    }

    /// <summary>One business per owner in the MVP; it starts Pending until a platform admin approves it.</summary>
    public async Task<OwnerBusinessResponse> RegisterAsync(Guid userId, SaveBusinessRequest request, CancellationToken cancellationToken)
    {
        if (society.SocietyId is null)
        {
            throw new AppException(ErrorKind.Forbidden, "Finish signing up before you start selling.");
        }

        if (await FindMineAsync(userId, cancellationToken) is not null)
        {
            throw new AppException(ErrorKind.Conflict, "You already have a business. Edit it from My business.");
        }

        var type = await db.BusinessTypes.SingleOrDefaultAsync(t => t.Id == request.BusinessTypeId && t.IsActive, cancellationToken)
            ?? throw new AppException(ErrorKind.Validation, "Choose what kind of business you run.");
        var category = await FindCategoryAsync(type.Id, request.CategoryId, cancellationToken);
        var ownerPhone = await db.Users.Where(u => u.Id == userId).Select(u => u.Phone).SingleAsync(cancellationToken);

        var business = new Business(userId, type, category, ToProfile(request, ownerPhone), time.GetUtcNow().UtcDateTime);
        db.Businesses.Add(business);
        await db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(business, cancellationToken);
    }

    /// <summary>Profile edits; a rejected business goes back to Pending for another review.</summary>
    public async Task<OwnerBusinessResponse> UpdateProfileAsync(Guid userId, SaveBusinessRequest request, CancellationToken cancellationToken)
    {
        var business = await FindEditableAsync(userId, cancellationToken);
        if (request.BusinessTypeId != business.BusinessTypeId)
        {
            throw new AppException(ErrorKind.Validation, "The kind of business cannot be changed. Please contact the TowerHaat team.");
        }

        var category = await FindCategoryAsync(business.BusinessTypeId, request.CategoryId, cancellationToken);
        var ownerPhone = await db.Users.Where(u => u.Id == userId).Select(u => u.Phone).SingleAsync(cancellationToken);

        business.UpdateProfile(category, ToProfile(request, ownerPhone));
        await db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(business, cancellationToken);
    }

    public async Task<OwnerBusinessResponse> UpdateSettingsAsync(Guid userId, BusinessSettingsRequest request, CancellationToken cancellationToken)
    {
        var business = await FindEditableAsync(userId, cancellationToken);

        var opensAt = ParseTime(request.OpensAt, "Opening time");
        var closesAt = ParseTime(request.ClosesAt, "Closing time");
        TimeOnly? until = string.IsNullOrWhiteSpace(request.AcceptOrdersUntil)
            ? null
            : ParseTime(request.AcceptOrdersUntil, "Last order time");
        var hours = new BusinessHours(opensAt, closesAt, until);

        if (until is { } cutOff && !hours.Contains(cutOff))
        {
            throw new AppException(ErrorKind.Validation, "The last order time must be within your opening hours.");
        }

        business.UpdateSettings(
            hours,
            Validate.Amount(request.MinOrderValue, "Minimum order", MaxAmount),
            Validate.Amount(request.DeliveryCharge, "Delivery charge", MaxAmount));
        await db.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(business, cancellationToken);
    }

    public async Task<OwnerBusinessResponse> SetOpenAsync(Guid userId, bool isOpen, CancellationToken cancellationToken)
    {
        var business = await FindEditableAsync(userId, cancellationToken);
        business.SetOpen(isOpen);
        await db.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(business, cancellationToken);
    }

    public async Task<PhotoResponse> AddPhotoAsync(Guid userId, Stream content, long length, CancellationToken cancellationToken)
    {
        var business = await FindEditableAsync(userId, cancellationToken);
        if (business.Photos.Count >= Business.MaxPhotos)
        {
            throw new AppException(ErrorKind.Validation, $"You can add up to {Business.MaxPhotos} photos. Remove one first.");
        }

        var (image, extension) = await ImageUpload.ReadAsync(content, length, cancellationToken);
        await using (image)
        {
            var key = await files.SaveAsync(image, extension, cancellationToken);
            var photo = business.AddPhoto(key);
            db.BusinessPhotos.Add(photo);
            await db.SaveChangesAsync(cancellationToken);
            return new PhotoResponse(photo.Id, ImageUpload.Url(key)!);
        }
    }

    public async Task DeletePhotoAsync(Guid userId, Guid photoId, CancellationToken cancellationToken)
    {
        var business = await FindEditableAsync(userId, cancellationToken);
        var photo = business.Photos.SingleOrDefault(p => p.Id == photoId)
            ?? throw new AppException(ErrorKind.NotFound, "Photo not found.");

        business.RemovePhoto(photo);
        db.BusinessPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        await files.DeleteAsync(photo.FileKey, cancellationToken);
    }

    /// <summary>The owner's business in their own society (the tenancy filter applies).</summary>
    internal Task<Business?> FindMineAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Businesses.Include(b => b.Photos).SingleOrDefaultAsync(b => b.OwnerUserId == userId, cancellationToken);

    internal async Task<Business> FindEditableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var business = await FindMineAsync(userId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "You have not registered a business yet.");

        return business.Status == BusinessStatus.Deactivated
            ? throw new AppException(ErrorKind.Forbidden, "Your business has been deactivated. Please contact the TowerHaat team.")
            : business;
    }

    private async Task<Category> FindCategoryAsync(Guid typeId, Guid categoryId, CancellationToken cancellationToken) =>
        await db.Categories.SingleOrDefaultAsync(
            c => c.Id == categoryId && c.BusinessTypeId == typeId && c.IsActive, cancellationToken)
        ?? throw new AppException(ErrorKind.Validation, "Choose a category.");

    private static BusinessProfile ToProfile(SaveBusinessRequest request, string ownerPhone)
    {
        var whatsApp = ownerPhone;
        if (!string.IsNullOrWhiteSpace(request.WhatsAppPhone) && !PhoneNumber.TryNormalize(request.WhatsAppPhone, out whatsApp))
        {
            throw new AppException(ErrorKind.Validation, "Enter a valid 10-digit WhatsApp number.");
        }

        return new BusinessProfile(
            Validate.Required(request.Name, "Business name", MaxNameLength),
            Validate.Optional(request.Description, "Description", MaxDescriptionLength),
            Validate.UpiId(request.UpiId),
            Validate.FssaiNumber(request.FssaiNumber),
            whatsApp);
    }

    private static TimeOnly ParseTime(string? value, string field) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new AppException(ErrorKind.Validation, $"{field} is not a valid time.");

    private async Task<OwnerBusinessResponse> ToResponseAsync(Business business, CancellationToken cancellationToken)
    {
        var type = await db.BusinessTypes.SingleAsync(t => t.Id == business.BusinessTypeId, cancellationToken);
        var category = await db.Categories.SingleAsync(c => c.Id == business.CategoryId, cancellationToken);

        return new OwnerBusinessResponse(
            business.Id,
            business.Name,
            business.Description,
            type.Id,
            type.Name,
            type.OrderingEnabled,
            category.Id,
            category.Name,
            business.UpiId,
            business.FssaiNumber,
            business.WhatsAppPhone,
            business.Status,
            business.StatusNote,
            Settings(business),
            business.IsOpenAt(IndiaTime.TimeNow(time)),
            business.Photos.OrderBy(p => p.SortOrder).Select(p => new PhotoResponse(p.Id, ImageUpload.Url(p.FileKey)!)).ToList());
    }

    internal static BusinessSettingsResponse Settings(Business business) => new(
        Format(business.Hours.OpensAt),
        Format(business.Hours.ClosesAt),
        business.Hours.AcceptOrdersUntil is { } until ? Format(until) : null,
        business.IsOpen,
        business.MinOrderValue,
        business.DeliveryCharge);

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
