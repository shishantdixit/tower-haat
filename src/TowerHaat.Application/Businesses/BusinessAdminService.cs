using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Media;
using TowerHaat.Application.Tenancy;
using TowerHaat.Domain.Businesses;

namespace TowerHaat.Application.Businesses;

/// <summary>Platform admin approval queue (A-02): approve, reject or deactivate with a note.</summary>
public sealed class BusinessAdminService(IAppDbContext db, ISocietyScope scope, TimeProvider time)
{
    public const int MaxNoteLength = 500;

    /// <summary>Businesses of every society, oldest first, so the queue is worked in order.</summary>
    public async Task<List<AdminBusinessResponse>> ListAsync(BusinessStatus? status, CancellationToken cancellationToken)
    {
        // A platform admin view across societies, so the tenancy filter is ignored on purpose.
        var businesses = await db.Businesses
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Include(b => b.Photos)
            .Where(b => status == null || b.Status == status)
            .ToListAsync(cancellationToken);

        var ownerIds = businesses.Select(b => b.OwnerUserId).Distinct().ToList();
        var owners = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        var addresses = await db.ResidentProfiles
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Include(p => p.Flat!).ThenInclude(f => f.Tower)
            .Where(p => ownerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);
        var societies = await db.Societies.ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var types = await db.BusinessTypes.ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return businesses
            .OrderBy(b => b.CreatedAt)
            .Select(b =>
            {
                var owner = owners[b.OwnerUserId];
                var address = addresses.GetValueOrDefault(b.OwnerUserId);
                var addressText = address is null
                    ? string.Empty
                    : $"{address.Flat?.Tower.Name ?? address.ManualTower}-{address.Flat?.Number ?? address.ManualFlat}";

                return new AdminBusinessResponse(
                    b.Id,
                    b.Name,
                    b.Description,
                    b.SocietyId,
                    societies.GetValueOrDefault(b.SocietyId, string.Empty),
                    owner.Name ?? string.Empty,
                    owner.Phone,
                    addressText,
                    types.GetValueOrDefault(b.BusinessTypeId, string.Empty),
                    categories.GetValueOrDefault(b.CategoryId, string.Empty),
                    b.UpiId,
                    b.FssaiNumber,
                    b.Status,
                    b.StatusNote,
                    b.CreatedAt,
                    b.Photos.OrderBy(p => p.SortOrder).Select(p => ImageUpload.Url(p.FileKey)!).ToList());
            })
            .ToList();
    }

    public Task ApproveAsync(Guid businessId, BusinessDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(businessId, b => b.Approve(Note(request, required: false), time.GetUtcNow().UtcDateTime), cancellationToken);

    public Task RejectAsync(Guid businessId, BusinessDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(businessId, b => b.Reject(Note(request, required: true)!), cancellationToken);

    public Task DeactivateAsync(Guid businessId, BusinessDecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(businessId, b => b.Deactivate(Note(request, required: true)!), cancellationToken);

    private async Task DecideAsync(Guid businessId, Action<Business> decide, CancellationToken cancellationToken)
    {
        var societyId = await db.Businesses
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Where(b => b.Id == businessId)
            .Select(b => (Guid?)b.SocietyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Business not found.");

        // Work inside the business's society so the save passes the tenancy checks.
        scope.Use(societyId);
        var business = await db.Businesses.SingleAsync(b => b.Id == businessId, cancellationToken);

        try
        {
            decide(business);
        }
        catch (InvalidOperationException e)
        {
            throw new AppException(ErrorKind.Conflict, e.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Note(BusinessDecisionRequest request, bool required)
    {
        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note))
        {
            return required ? throw new AppException(ErrorKind.Validation, "Add a note for the owner.") : null;
        }

        return note.Length <= MaxNoteLength
            ? note
            : throw new AppException(ErrorKind.Validation, $"The note can be at most {MaxNoteLength} characters.");
    }
}
