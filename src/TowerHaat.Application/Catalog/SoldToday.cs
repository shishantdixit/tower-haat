using TowerHaat.Application.Abstractions;

namespace TowerHaat.Application.Catalog;

/// <summary>How many of each item a business has sold today, for daily limits (C-04).</summary>
internal static class SoldToday
{
    // Ordering arrives in M3, which counts today's order items here; until then nothing is sold.
    public static Task<Dictionary<Guid, int>> ForBusinessAsync(
        IAppDbContext db, Guid businessId, TimeProvider time, CancellationToken cancellationToken) =>
        Task.FromResult(new Dictionary<Guid, int>());
}
