using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Directory;
using TowerHaat.Application.Tenancy;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Societies;
using TowerHaat.Domain.Users;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Tests.Support;

/// <summary>Residents, businesses and the M2 services over a test database, one fresh request at a time.</summary>
public sealed class BusinessHarness : IDisposable
{
    public static readonly Guid Food = new("01920000-0000-7000-8001-000000000001");
    public static readonly Guid HomeKitchen = new("01920000-0000-7000-8002-000000000101");
    public static readonly Guid Bakery = new("01920000-0000-7000-8002-000000000103");
    public static readonly Guid Tuition = new("01920000-0000-7000-8001-000000000003");
    public static readonly Guid Music = new("01920000-0000-7000-8002-000000000302");

    private int _phones;

    public BusinessHarness()
    {
        using var db = Database.CreateContext(societyId: null);
        db.Societies.Add(OtherSociety);
        db.SaveChanges();
    }

    public TestDatabase Database { get; } = new();

    /// <summary>11:30 in India: inside the default 08:00 to 21:00 hours.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 25, 6, 0, 0, TimeSpan.Zero));

    public InMemoryFileStorage Files { get; } = new();

    public Society OtherSociety { get; } = new("Other Society", "Noida");

    public CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A signed-up resident (user plus profile) of the given society.</summary>
    public async Task<Guid> ResidentAsync(Guid? societyId = null)
    {
        var society = societyId ?? Society.AceDivinoId;
        await using var db = Database.CreateContext(society);
        var user = new User($"+9198765{++_phones:D5}", DateTime.UtcNow);
        user.Rename($"Resident {_phones}");
        db.Users.Add(user);
        db.ResidentProfiles.Add(new ResidentProfile(user.Id, society, DateTime.UtcNow));
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    /// <summary>A resident with a registered business, approved unless said otherwise.</summary>
    public async Task<(Guid OwnerId, Guid BusinessId)> BusinessAsync(
        string name = "Asha's Kitchen", Guid? societyId = null, bool approve = true, Guid? typeId = null, Guid? categoryId = null)
    {
        var society = societyId ?? Society.AceDivinoId;
        var owner = await ResidentAsync(society);
        var registered = await As(society).Owner.RegisterAsync(
            owner,
            new SaveBusinessRequest(typeId ?? Food, categoryId ?? HomeKitchen, name, "Home food", "asha@okaxis", null, null),
            Ct);

        if (approve)
        {
            await Admin().Admin.ApproveAsync(registered.Id, new BusinessDecisionRequest(null), Ct);
        }

        return (owner, registered.Id);
    }

    /// <summary>Services for one request by a resident of <paramref name="societyId"/>.</summary>
    public Services As(Guid? societyId) => Create(TestDatabase.Society(societyId));

    /// <summary>Services for one request by a platform admin who lives in another society.</summary>
    public Services Admin() => Create(TestDatabase.Society(Guid.CreateVersion7(), canUseAnySociety: true));

    public async Task<Business> LoadAsync(Guid businessId)
    {
        await using var db = Database.CreateContext(societyId: null);
        return await db.Businesses.IgnoreQueryFilters().Include(b => b.Photos).SingleAsync(b => b.Id == businessId, Ct);
    }

    public void Dispose() => Database.Dispose();

    private Services Create(SocietyContext society)
    {
        var db = Database.CreateContext(society);
        var owner = new OwnerBusinessService(db, society, Files, Time);
        return new Services(
            db,
            owner,
            new CatalogService(db, owner, Files, Time),
            new BusinessAdminService(db, society, Time),
            new BusinessTypeService(db),
            new DirectoryService(db, society, Time));
    }

    public sealed record Services(
        AppDbContext Db,
        OwnerBusinessService Owner,
        CatalogService Catalog,
        BusinessAdminService Admin,
        BusinessTypeService Types,
        DirectoryService Directory);
}
