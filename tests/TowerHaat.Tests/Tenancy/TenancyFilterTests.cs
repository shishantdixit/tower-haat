using Microsoft.EntityFrameworkCore;
using TowerHaat.Domain.Societies;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Tenancy;

public sealed class TenancyFilterTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Society _aceDivino = new("Ace Divino", "Noida");
    private readonly Society _other = new("Other Society", "Noida");

    public TenancyFilterTests()
    {
        using var context = _database.CreateContext(societyId: null);
        context.Societies.AddRange(_aceDivino, _other);
        context.Towers.AddRange(
            new Tower("A") { SocietyId = _aceDivino.Id },
            new Tower("B") { SocietyId = _aceDivino.Id },
            new Tower("A") { SocietyId = _other.Id });
        context.SaveChanges();
    }

    [Fact]
    public async Task Resident_sees_only_their_societys_rows()
    {
        await using var context = _database.CreateContext(_aceDivino.Id);

        var towers = await context.Towers.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, towers.Count);
        Assert.All(towers, t => Assert.Equal(_aceDivino.Id, t.SocietyId));
    }

    [Fact]
    public async Task Filter_cannot_be_bypassed_by_filtering_on_another_society()
    {
        await using var context = _database.CreateContext(_aceDivino.Id);

        var towers = await context.Towers
            .Where(t => t.SocietyId == _other.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(towers);
    }

    [Fact]
    public async Task No_current_society_sees_no_tenant_rows()
    {
        await using var context = _database.CreateContext(societyId: null);

        Assert.Empty(await context.Towers.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Platform_admin_query_ignoring_the_filter_sees_every_society()
    {
        await using var context = _database.CreateContext(societyId: null);

        var count = await context.Towers
            .IgnoreQueryFilters([AppDbContext.TenancyFilter])
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Societies_themselves_are_not_filtered()
    {
        await using var context = _database.CreateContext(_aceDivino.Id);

        Assert.Equal(2, await context.Societies.CountAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose() => _database.Dispose();
}
