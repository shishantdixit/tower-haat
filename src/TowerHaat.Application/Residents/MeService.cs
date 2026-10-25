using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Tenancy;
using TowerHaat.Domain.Users;

namespace TowerHaat.Application.Residents;

/// <summary>The signed-in user and where they live (C-01).</summary>
public sealed class MeService(IAppDbContext db, ISocietyScope scope, IAccessTokenIssuer accessTokens, TimeProvider time)
{
    public const int MaxNameLength = 80;
    public const int MaxManualTowerLength = 50;
    public const int MaxManualFlatLength = 20;

    public async Task<MeResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AppException(ErrorKind.Unauthorized, "Please sign in again.");

        return await ToResponseAsync(user, await LoadOwnProfileAsync(userId, cancellationToken), cancellationToken);
    }

    public async Task<SaveProfileResponse> SaveProfileAsync(Guid userId, SaveProfileRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > MaxNameLength)
        {
            throw new AppException(ErrorKind.Validation, $"Enter your name (2 to {MaxNameLength} characters).");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AppException(ErrorKind.Unauthorized, "Please sign in again.");

        var profile = await LoadOwnProfileAsync(userId, cancellationToken);
        if (profile is not null && profile.SocietyId != request.SocietyId)
        {
            throw new AppException(ErrorKind.Conflict, "Moving to another society is not supported yet.");
        }

        if (profile is null && !request.Consent)
        {
            throw new AppException(
                ErrorKind.Validation, "Please agree to share your name, phone, tower and flat with businesses you order from.");
        }

        var society = await db.Societies.SingleOrDefaultAsync(s => s.Id == request.SocietyId, cancellationToken);
        if (society is null || !society.IsActive)
        {
            throw new AppException(ErrorKind.Validation, "Choose your society.");
        }

        scope.Use(society.Id);

        if (profile is null)
        {
            profile = new ResidentProfile(user.Id, society.Id, time.GetUtcNow().UtcDateTime);
            db.ResidentProfiles.Add(profile);
        }

        await SetAddressAsync(profile, request, cancellationToken);
        user.Rename(name);
        await db.SaveChangesAsync(cancellationToken);

        var (access, me) = await IssueAccessTokenAsync(user, cancellationToken);
        return new SaveProfileResponse(access.Token, access.ExpiresAt, me);
    }

    /// <summary>Access tokens carry the user's society, so a new one is issued whenever it may change.</summary>
    public async Task<(AccessToken Token, MeResponse Me)> IssueAccessTokenAsync(User user, CancellationToken cancellationToken)
    {
        var profile = await LoadOwnProfileAsync(user.Id, cancellationToken);
        var token = accessTokens.Issue(new AccessTokenClaims(user.Id, user.Phone, profile?.SocietyId, user.IsPlatformAdmin));
        return (token, await ToResponseAsync(user, profile, cancellationToken));
    }

    private async Task SetAddressAsync(ResidentProfile profile, SaveProfileRequest request, CancellationToken cancellationToken)
    {
        var manualTower = request.ManualTower?.Trim() ?? string.Empty;
        var manualFlat = request.ManualFlat?.Trim() ?? string.Empty;
        var hasManual = manualTower.Length > 0 || manualFlat.Length > 0;

        if (request.FlatId is { } flatId)
        {
            if (hasManual)
            {
                throw new AppException(ErrorKind.Validation, "Pick your flat from the list or type it, not both.");
            }

            // Filtered to the society in scope, so a flat of another society is not found.
            var flat = await db.Flats.Include(f => f.Tower).SingleOrDefaultAsync(f => f.Id == flatId, cancellationToken)
                ?? throw new AppException(ErrorKind.Validation, "That flat is not in your society's list.");

            profile.LiveIn(flat);
            return;
        }

        if (manualTower.Length == 0 || manualFlat.Length == 0)
        {
            throw new AppException(ErrorKind.Validation, "Pick your tower and flat.");
        }

        if (manualTower.Length > MaxManualTowerLength || manualFlat.Length > MaxManualFlatLength)
        {
            throw new AppException(ErrorKind.Validation, "Tower or flat is too long.");
        }

        // Typed text that matches the list is stored as the listed flat.
        var towerKey = manualTower.ToUpper();
        var flatKey = manualFlat.ToUpper();
        var listed = await db.Flats
            .Include(f => f.Tower)
            .FirstOrDefaultAsync(f => f.Tower.Name.ToUpper() == towerKey && f.Number.ToUpper() == flatKey, cancellationToken);

        if (listed is not null)
        {
            profile.LiveIn(listed);
        }
        else
        {
            profile.LiveInUnlistedFlat(manualTower, manualFlat);
        }
    }

    /// <summary>
    /// The user's own profile, read before the request has a society (sign-in, token refresh).
    /// The one non-admin place the tenancy filter is bypassed, and only by the user's own id.
    /// </summary>
    private Task<ResidentProfile?> LoadOwnProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        db.ResidentProfiles
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Include(p => p.Flat!).ThenInclude(f => f.Tower)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    private async Task<MeResponse> ToResponseAsync(User user, ResidentProfile? profile, CancellationToken cancellationToken)
    {
        ProfileResponse? profileResponse = null;
        if (profile is not null)
        {
            var societyName = await db.Societies
                .Where(s => s.Id == profile.SocietyId)
                .Select(s => s.Name)
                .SingleAsync(cancellationToken);
            profileResponse = new ProfileResponse(
                profile.SocietyId,
                societyName,
                profile.FlatId,
                profile.Flat?.Tower.Name ?? profile.ManualTower ?? string.Empty,
                profile.Flat?.Number ?? profile.ManualFlat ?? string.Empty,
                profile.FlatId is not null);
        }

        return new MeResponse(user.Id, user.Phone, user.Name, user.IsPlatformAdmin, profileResponse);
    }
}
