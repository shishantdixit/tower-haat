using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Common;
using TowerHaat.Application.Societies;
using TowerHaat.Domain.Societies;
using TowerHaat.Domain.Users;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Societies;

/// <summary>Platform admin: societies (A-01) and the tower and flat list (A-06).</summary>
public sealed class SocietyAdminTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ace_Divino_is_seeded_and_active()
    {
        var societies = await Admin().ListAsync(_ct);

        var society = Assert.Single(societies);
        Assert.Equal((Society.AceDivinoId, "Ace Divino", true), (society.Id, society.Name, society.IsActive));
    }

    [Fact]
    public async Task Create_activate_and_deactivate_a_society()
    {
        var created = await Admin().CreateAsync(new CreateSocietyRequest(" Supertech ", "Noida"), _ct);
        await Admin().SetActiveAsync(created.Id, isActive: false, _ct);

        var directory = await Directory().ListActiveAsync(_ct);

        Assert.Equal("Supertech", created.Name);
        Assert.DoesNotContain(directory, s => s.Id == created.Id);

        await Admin().SetActiveAsync(created.Id, isActive: true, _ct);
        directory = await Directory().ListActiveAsync(_ct);
        Assert.Contains(directory, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Society_names_are_unique()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => Admin().CreateAsync(new CreateSocietyRequest("ace divino", "Noida"), _ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Import_creates_towers_and_flats_and_skips_ones_already_listed()
    {
        const string csv = """
            Tower,Flat
            A,101
            A,102
            B,101

            a,101
            "B", "102"
            A,101
            """;

        var result = await Admin().ImportFlatsAsync(Society.AceDivinoId, new ImportFlatsRequest(csv), _ct);

        Assert.Equal((2, 4, 2), (result.TowersCreated, result.FlatsCreated, result.FlatsAlreadyListed));
        Assert.Empty(result.Errors);

        var again = await Admin().ImportFlatsAsync(Society.AceDivinoId, new ImportFlatsRequest(csv), _ct);
        Assert.Equal((0, 0, 6), (again.TowersCreated, again.FlatsCreated, again.FlatsAlreadyListed));

        var towers = await Admin().ListTowersAsync(Society.AceDivinoId, _ct);
        Assert.Equal(["A", "B"], towers.Select(t => t.Name));
        Assert.Equal(["101", "102"], towers[0].Flats.Select(f => f.Number));
    }

    [Fact]
    public async Task Import_reports_bad_lines_and_keeps_the_good_ones()
    {
        var result = await Admin().ImportFlatsAsync(
            Society.AceDivinoId, new ImportFlatsRequest("A,101\nA\nA;102;x\n" + new string('T', 51) + ",1\nB\t7"), _ct);

        Assert.Equal(2, result.FlatsCreated);
        Assert.Equal([2, 3, 4], result.Errors.Select(e => e.Line));
    }

    [Fact]
    public async Task Imported_flats_belong_to_the_chosen_society_only()
    {
        var other = await Admin().CreateAsync(new CreateSocietyRequest("Other", "Noida"), _ct);

        await Admin().ImportFlatsAsync(other.Id, new ImportFlatsRequest("A,101"), _ct);

        Assert.Empty(await Admin().ListTowersAsync(Society.AceDivinoId, _ct));
        await using var aceDivino = _database.CreateContext(Society.AceDivinoId);
        Assert.Empty(await aceDivino.Flats.ToListAsync(_ct));
    }

    [Fact]
    public async Task Add_and_remove_towers_and_flats()
    {
        var tower = await Admin().AddTowerAsync(Society.AceDivinoId, new CreateTowerRequest("A"), _ct);
        var flat = await Admin().AddFlatAsync(Society.AceDivinoId, tower.Id, new CreateFlatRequest("101"), _ct);

        await Assert.ThrowsAsync<AppException>(
            () => Admin().AddTowerAsync(Society.AceDivinoId, new CreateTowerRequest("a"), _ct));
        await Assert.ThrowsAsync<AppException>(
            () => Admin().AddFlatAsync(Society.AceDivinoId, tower.Id, new CreateFlatRequest("101"), _ct));

        await Admin().DeleteFlatAsync(Society.AceDivinoId, flat.Id, _ct);
        await Admin().AddFlatAsync(Society.AceDivinoId, tower.Id, new CreateFlatRequest("102"), _ct);
        await Admin().DeleteTowerAsync(Society.AceDivinoId, tower.Id, _ct);

        Assert.Empty(await Admin().ListTowersAsync(Society.AceDivinoId, _ct));
    }

    [Fact]
    public async Task A_flat_or_tower_where_a_resident_lives_cannot_be_removed()
    {
        await Admin().ImportFlatsAsync(Society.AceDivinoId, new ImportFlatsRequest("A,101"), _ct);
        await using (var db = _database.CreateContext(Society.AceDivinoId))
        {
            var flat = await db.Flats.SingleAsync(_ct);
            var user = new User("+919876543210", DateTime.UtcNow);
            var profile = new ResidentProfile(user.Id, Society.AceDivinoId, DateTime.UtcNow);
            profile.LiveIn(flat);
            db.AddRange(user, profile);
            await db.SaveChangesAsync(_ct);
        }

        var towers = await Admin().ListTowersAsync(Society.AceDivinoId, _ct);

        await Assert.ThrowsAsync<AppException>(
            () => Admin().DeleteFlatAsync(Society.AceDivinoId, towers[0].Flats[0].Id, _ct));
        await Assert.ThrowsAsync<AppException>(
            () => Admin().DeleteTowerAsync(Society.AceDivinoId, towers[0].Id, _ct));
    }

    [Fact]
    public async Task Society_counts_cover_every_society()
    {
        await Admin().ImportFlatsAsync(Society.AceDivinoId, new ImportFlatsRequest("A,101\nA,102\nB,1"), _ct);

        var aceDivino = (await Admin().ListAsync(_ct)).Single();

        Assert.Equal((2, 3, 0), (aceDivino.Towers, aceDivino.Flats, aceDivino.Residents));
    }

    [Fact]
    public async Task Picker_lists_towers_of_an_active_society_only()
    {
        await Admin().ImportFlatsAsync(Society.AceDivinoId, new ImportFlatsRequest("A,101"), _ct);
        var towers = await Directory().ListTowersAsync(Society.AceDivinoId, _ct);
        Assert.Equal("A", Assert.Single(towers).Name);

        await Admin().SetActiveAsync(Society.AceDivinoId, isActive: false, _ct);
        await Assert.ThrowsAsync<AppException>(
            () => Directory().ListTowersAsync(Society.AceDivinoId, _ct));
    }

    /// <summary>A fresh request from someone who has not signed up yet.</summary>
    private SocietyDirectoryService Directory()
    {
        var society = TestDatabase.Society(societyId: null);
        return new SocietyDirectoryService(_database.CreateContext(society), society);
    }

    /// <summary>A fresh admin request, signed in as an admin who also lives in another society.</summary>
    private SocietyAdminService Admin()
    {
        var society = TestDatabase.Society(Guid.CreateVersion7(), canUseAnySociety: true);
        return new SocietyAdminService(_database.CreateContext(society), society);
    }

    public void Dispose() => _database.Dispose();
}
