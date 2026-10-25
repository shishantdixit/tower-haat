using Microsoft.EntityFrameworkCore;
using TowerHaat.Domain.Societies;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Tenancy;

public sealed class TenancyWriteTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Society _home = new("Test Society", "Noida");
    private readonly Society _other = new("Other Society", "Noida");

    public TenancyWriteTests()
    {
        using var context = _database.CreateContext(societyId: null);
        context.Societies.AddRange(_home, _other);
        context.SaveChanges();
    }

    [Fact]
    public async Task New_row_gets_the_current_society()
    {
        var ct = TestContext.Current.CancellationToken;
        var tower = new Tower("C");

        await using (var context = _database.CreateContext(_home.Id))
        {
            context.Towers.Add(tower);
            await context.SaveChangesAsync(ct);
        }

        await using var admin = _database.CreateContext(societyId: null);
        var saved = await admin.Towers.IgnoreQueryFilters().SingleAsync(t => t.Id == tower.Id, ct);
        Assert.Equal(_home.Id, saved.SocietyId);
    }

    [Fact]
    public async Task Cannot_add_a_row_to_another_society()
    {
        await using var context = _database.CreateContext(_home.Id);
        context.Towers.Add(new Tower("C") { SocietyId = _other.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cannot_add_a_row_without_any_society()
    {
        await using var context = _database.CreateContext(societyId: null);
        context.Towers.Add(new Tower("C"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_current_society_an_explicit_society_is_allowed()
    {
        // Sign-up and platform admin write rows before a resident has a society claim.
        await using var context = _database.CreateContext(societyId: null);
        context.Towers.Add(new Tower("C") { SocietyId = _other.Id });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cannot_move_a_row_to_another_society()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _database.CreateContext(_home.Id);
        var tower = new Tower("C");
        context.Towers.Add(tower);
        await context.SaveChangesAsync(ct);

        tower.SocietyId = _other.Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Cannot_change_or_delete_another_societys_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var foreign = new Tower("C") { SocietyId = _other.Id };
        await using (var setup = _database.CreateContext(societyId: null))
        {
            setup.Towers.Add(foreign);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = _database.CreateContext(_home.Id);
        context.Towers.Attach(foreign);
        context.Towers.Remove(foreign);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(ct));
    }

    public void Dispose() => _database.Dispose();
}
