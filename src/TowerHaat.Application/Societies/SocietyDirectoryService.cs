using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;

namespace TowerHaat.Application.Societies;

/// <summary>Public lists for the sign-up picker: active societies and their towers and flats (C-01).</summary>
public sealed class SocietyDirectoryService(IAppDbContext db, ISocietyScope scope)
{
    public Task<List<SocietyResponse>> ListActiveAsync(CancellationToken cancellationToken) =>
        db.Societies
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SocietyResponse(s.Id, s.Name, s.City))
            .ToListAsync(cancellationToken);

    public async Task<List<TowerResponse>> ListTowersAsync(Guid societyId, CancellationToken cancellationToken)
    {
        if (!await db.Societies.AnyAsync(s => s.Id == societyId && s.IsActive, cancellationToken))
        {
            throw new AppException(ErrorKind.NotFound, "Society not found.");
        }

        scope.Use(societyId);
        return await TowerLists.LoadAsync(db, cancellationToken);
    }
}

internal static class TowerLists
{
    /// <summary>Towers and flats of the society in scope, in natural order (A-2 before A-10).</summary>
    public static async Task<List<TowerResponse>> LoadAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var towers = await db.Towers
            .Select(t => new { t.Id, t.Name, Flats = t.Flats.Select(f => new FlatResponse(f.Id, f.Number)).ToList() })
            .ToListAsync(cancellationToken);

        return towers
            .OrderBy(t => t.Name, NaturalComparer.Instance)
            .Select(t => new TowerResponse(t.Id, t.Name, t.Flats.OrderBy(f => f.Number, NaturalComparer.Instance).ToList()))
            .ToList();
    }
}
