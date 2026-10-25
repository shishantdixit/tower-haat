using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Tenancy;

namespace TowerHaat.Application.Orders;

/// <summary>
/// Expires overdue orders in every society. Called by the in-process timer and by the hosting
/// provider's scheduled task (the app may be unloaded when idle). Each society runs in its own
/// scope, so the tenancy filter stays on throughout.
/// </summary>
public sealed class OrderExpiryJob(IServiceScopeFactory scopes)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        List<Guid> societyIds;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            societyIds = await db.Societies.Select(s => s.Id).ToListAsync(cancellationToken);
        }

        var expired = 0;
        foreach (var societyId in societyIds)
        {
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SocietyContext>();
            context.SetHome(null, canUseAnySociety: true);
            context.Use(societyId);
            expired += await scope.ServiceProvider.GetRequiredService<OrderViews>().ExpireDueAsync(cancellationToken);
        }

        return expired;
    }
}
