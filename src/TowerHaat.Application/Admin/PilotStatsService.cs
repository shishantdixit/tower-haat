using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Tenancy;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Admin;

public sealed record PilotStatsResponse(
    int ActiveBusinesses,
    int Residents,
    int CustomersInPeriod,
    IReadOnlyList<DayStats> Days,
    IReadOnlyList<BusinessStats> Businesses);

public sealed record DayStats(DateOnly Date, int Placed, int Delivered, int Rejected, int Expired, int Cancelled, int Disputed);

public sealed record BusinessStats(string Name, int Orders, int Delivered, int RejectedOrExpired, decimal DeliveredValue);

/// <summary>
/// What the pilot is measured by: orders per day and how they ended, payment disputes, and how each
/// business is doing. Every society, so a platform admin view outside the tenancy filter.
/// </summary>
public sealed class PilotStatsService(IAppDbContext db, TimeProvider time)
{
    public async Task<PilotStatsResponse> GetAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 90);
        var today = IndiaTime.Today(time);
        var since = IndiaTime.ToUtc(today.AddDays(1 - days), TimeOnly.MinValue);

        var orders = await db.Orders
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Include(o => o.Payment)
            .Where(o => o.PlacedAt >= since)
            .Select(o => new { o.PlacedAt, o.Status, Payment = o.Payment.Status, o.BusinessId, o.CustomerUserId, o.Total })
            .ToListAsync(cancellationToken);
        var businesses = await db.Businesses
            .IgnoreQueryFilters([TenancyFilter.Name])
            .Select(b => new { b.Id, b.Name, b.Status })
            .ToListAsync(cancellationToken);
        var residents = await db.ResidentProfiles.IgnoreQueryFilters([TenancyFilter.Name]).CountAsync(cancellationToken);

        var byDay = orders.ToLookup(o => IndiaTime.Date(o.PlacedAt));
        var dayStats = Enumerable.Range(0, days)
            .Select(i => today.AddDays(-i))
            .Select(date =>
            {
                var day = byDay[date].ToList();
                return new DayStats(
                    date,
                    day.Count,
                    day.Count(o => o.Status == OrderStatus.Delivered),
                    day.Count(o => o.Status == OrderStatus.Rejected),
                    day.Count(o => o.Status == OrderStatus.Expired),
                    day.Count(o => o.Status is OrderStatus.CancelledByCustomer or OrderStatus.CancelledByOwner),
                    day.Count(o => o.Payment == PaymentStatus.Disputed));
            })
            .ToList();

        var names = businesses.ToDictionary(b => b.Id, b => b.Name);
        var businessStats = orders
            .GroupBy(o => o.BusinessId)
            .Select(g => new BusinessStats(
                names.GetValueOrDefault(g.Key, "?"),
                g.Count(),
                g.Count(o => o.Status == OrderStatus.Delivered),
                g.Count(o => o.Status is OrderStatus.Rejected or OrderStatus.Expired),
                g.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total)))
            .OrderByDescending(b => b.Orders)
            .ToList();

        return new PilotStatsResponse(
            businesses.Count(b => b.Status == BusinessStatus.Active),
            residents,
            orders.Select(o => o.CustomerUserId).Distinct().Count(),
            dayStats,
            businessStats);
    }
}
