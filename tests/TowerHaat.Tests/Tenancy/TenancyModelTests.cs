using Microsoft.EntityFrameworkCore;
using TowerHaat.Domain.Common;
using TowerHaat.Domain.Societies;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Tenancy;

/// <summary>Guards rule 1 of CLAUDE.md as new entities are added.</summary>
public sealed class TenancyModelTests : IDisposable
{
    // Entities shared by every society. Adding one here is a deliberate, reviewed decision.
    private static readonly HashSet<Type> GlobalEntities =
    [
        typeof(Society),
    ];

    private readonly TestDatabase _database = new();

    [Fact]
    public void Every_entity_is_tenant_owned_or_explicitly_global()
    {
        using var context = _database.CreateContext(societyId: null);

        var unscoped = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => !typeof(ITenantEntity).IsAssignableFrom(t) && !GlobalEntities.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unscoped);
    }

    [Fact]
    public void Every_tenant_entity_has_the_tenancy_filter()
    {
        using var context = _database.CreateContext(societyId: null);

        var missing = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.FindDeclaredQueryFilter(AppDbContext.TenancyFilter) is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Empty(missing);
    }

    public void Dispose() => _database.Dispose();
}
