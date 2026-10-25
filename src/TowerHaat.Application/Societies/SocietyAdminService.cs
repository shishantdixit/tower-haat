using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Tenancy;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Application.Societies;

/// <summary>Platform admin: societies (A-01) and their tower and flat lists (A-06).</summary>
public sealed class SocietyAdminService(IAppDbContext db, ISocietyScope scope)
{
    public const int MaxTowerLength = 50;
    public const int MaxFlatLength = 20;
    public const int MaxImportLines = 5000;

    public async Task<List<AdminSocietyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        // Counts span every society: a platform admin view, so the tenancy filter is ignored on purpose.
        var towers = await CountBySocietyAsync(db.Towers.IgnoreQueryFilters([TenancyFilter.Name]).Select(t => t.SocietyId), cancellationToken);
        var flats = await CountBySocietyAsync(db.Flats.IgnoreQueryFilters([TenancyFilter.Name]).Select(f => f.SocietyId), cancellationToken);
        var residents = await CountBySocietyAsync(
            db.ResidentProfiles.IgnoreQueryFilters([TenancyFilter.Name]).Select(p => p.SocietyId), cancellationToken);

        var societies = await db.Societies.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return societies
            .Select(s => new AdminSocietyResponse(
                s.Id, s.Name, s.City, s.IsActive, towers.GetValueOrDefault(s.Id), flats.GetValueOrDefault(s.Id), residents.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<AdminSocietyResponse> CreateAsync(CreateSocietyRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "Society name", 200);
        var city = Required(request.City, "City", 100);

        var key = name.ToUpper();
        if (await db.Societies.AnyAsync(s => s.Name.ToUpper() == key, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"A society named {name} already exists.");
        }

        var society = new Society(name, city);
        db.Societies.Add(society);
        await db.SaveChangesAsync(cancellationToken);

        return new AdminSocietyResponse(society.Id, society.Name, society.City, society.IsActive, 0, 0, 0);
    }

    public async Task SetActiveAsync(Guid societyId, bool isActive, CancellationToken cancellationToken)
    {
        var society = await FindSocietyAsync(societyId, cancellationToken);
        if (isActive)
        {
            society.Activate();
        }
        else
        {
            society.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TowerResponse>> ListTowersAsync(Guid societyId, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);
        return await TowerLists.LoadAsync(db, cancellationToken);
    }

    public async Task<TowerResponse> AddTowerAsync(Guid societyId, CreateTowerRequest request, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);
        var name = Required(request.Name, "Tower name", MaxTowerLength);

        var key = name.ToUpper();
        if (await db.Towers.AnyAsync(t => t.Name.ToUpper() == key, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"Tower {name} is already listed.");
        }

        var tower = new Tower(name) { SocietyId = societyId };
        db.Towers.Add(tower);
        await db.SaveChangesAsync(cancellationToken);

        return new TowerResponse(tower.Id, tower.Name, []);
    }

    /// <summary>Removes a tower and its flats, unless a resident lives in one of them.</summary>
    public async Task DeleteTowerAsync(Guid societyId, Guid towerId, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);
        var tower = await db.Towers.Include(t => t.Flats).SingleOrDefaultAsync(t => t.Id == towerId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Tower not found.");

        if (await db.ResidentProfiles.AnyAsync(p => p.Flat != null && p.Flat.TowerId == towerId, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"Residents live in tower {tower.Name}, so it cannot be removed.");
        }

        db.Flats.RemoveRange(tower.Flats);
        db.Towers.Remove(tower);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FlatResponse> AddFlatAsync(Guid societyId, Guid towerId, CreateFlatRequest request, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);
        var number = Required(request.Number, "Flat number", MaxFlatLength);

        var tower = await db.Towers.Include(t => t.Flats).SingleOrDefaultAsync(t => t.Id == towerId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Tower not found.");

        if (tower.Flats.Any(f => string.Equals(f.Number, number, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppException(ErrorKind.Conflict, $"Flat {number} is already listed in tower {tower.Name}.");
        }

        var flat = new Flat(tower, number);
        db.Flats.Add(flat);
        await db.SaveChangesAsync(cancellationToken);

        return new FlatResponse(flat.Id, flat.Number);
    }

    public async Task DeleteFlatAsync(Guid societyId, Guid flatId, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);
        var flat = await db.Flats.SingleOrDefaultAsync(f => f.Id == flatId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Flat not found.");

        if (await db.ResidentProfiles.AnyAsync(p => p.FlatId == flatId, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"A resident lives in flat {flat.Number}, so it cannot be removed.");
        }

        db.Flats.Remove(flat);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds towers and flats from "Tower,Flat" lines (A-06). Pairs already listed are skipped,
    /// so the same file can be uploaded again after fixing errors.
    /// </summary>
    public async Task<ImportFlatsResponse> ImportFlatsAsync(Guid societyId, ImportFlatsRequest request, CancellationToken cancellationToken)
    {
        await UseSocietyAsync(societyId, cancellationToken);

        var lines = (request.Csv ?? string.Empty).ReplaceLineEndings("\n").Split('\n');
        if (lines.Length > MaxImportLines)
        {
            throw new AppException(ErrorKind.Validation, $"Upload at most {MaxImportLines} lines at a time.");
        }

        var towers = (await db.Towers.Include(t => t.Flats).ToListAsync(cancellationToken))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var errors = new List<ImportError>();
        int towersCreated = 0, flatsCreated = 0, alreadyListed = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0 || (i == 0 && IsHeader(line)))
            {
                continue;
            }

            var parts = line.Split([',', ';', '\t']).Select(p => p.Trim().Trim('"').Trim()).ToArray();
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                errors.Add(new ImportError(lineNumber, "Expected: Tower,Flat"));
                continue;
            }

            var (towerName, number) = (parts[0], parts[1]);
            if (towerName.Length > MaxTowerLength || number.Length > MaxFlatLength)
            {
                errors.Add(new ImportError(lineNumber, "Tower or flat is too long."));
                continue;
            }

            if (!towers.TryGetValue(towerName, out var tower))
            {
                tower = new Tower(towerName) { SocietyId = societyId };
                db.Towers.Add(tower);
                towers.Add(towerName, tower);
                towersCreated++;
            }

            if (tower.Flats.Any(f => string.Equals(f.Number, number, StringComparison.OrdinalIgnoreCase))
                || db.Flats.Local.Any(f => f.TowerId == tower.Id && string.Equals(f.Number, number, StringComparison.OrdinalIgnoreCase)))
            {
                alreadyListed++;
                continue;
            }

            db.Flats.Add(new Flat(tower, number));
            flatsCreated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportFlatsResponse(towersCreated, flatsCreated, alreadyListed, errors);
    }

    private static bool IsHeader(string line) =>
        line.StartsWith("tower", StringComparison.OrdinalIgnoreCase)
        && line.Contains("flat", StringComparison.OrdinalIgnoreCase);

    private static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new AppException(ErrorKind.Validation, $"{field} is required (at most {maxLength} characters).");
        }

        return trimmed;
    }

    private static async Task<Dictionary<Guid, int>> CountBySocietyAsync(IQueryable<Guid> societyIds, CancellationToken cancellationToken) =>
        await societyIds
            .GroupBy(id => id)
            .Select(g => new { SocietyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SocietyId, x => x.Count, cancellationToken);

    private async Task<Society> FindSocietyAsync(Guid societyId, CancellationToken cancellationToken) =>
        await db.Societies.SingleOrDefaultAsync(s => s.Id == societyId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Society not found.");

    private async Task UseSocietyAsync(Guid societyId, CancellationToken cancellationToken)
    {
        await FindSocietyAsync(societyId, cancellationToken);
        scope.Use(societyId);
    }
}
