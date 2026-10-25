using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Catalog;

/// <summary>
/// How many of each item a business has sold today (since midnight in India), for daily limits (C-04).
/// Rejected, expired and cancelled orders give their stock back.
/// </summary>
internal static class SoldToday
{
    public static async Task<Dictionary<Guid, int>> ForBusinessAsync(
        IAppDbContext db, Guid businessId, TimeProvider time, CancellationToken cancellationToken)
    {
        var since = IndiaTime.StartOfTodayUtc(time);
        var lines = await db.OrderItems
            .Where(i => db.Orders.Any(o => o.Id == i.OrderId
                && o.BusinessId == businessId
                && o.PlacedAt >= since
                && OrderStatuses.CountTowardsStock.Contains(o.Status)))
            .Select(i => new { i.CatalogItemId, i.Quantity })
            .ToListAsync(cancellationToken);

        return lines.GroupBy(l => l.CatalogItemId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
    }
}
