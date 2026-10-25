using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Domain.Auth;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Common;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentSociety currentSociety)
    : DbContext(options), IAppDbContext
{
    public const string TenancyFilter = Application.Tenancy.TenancyFilter.Name;

    public DbSet<Society> Societies => Set<Society>();

    public DbSet<Tower> Towers => Set<Tower>();

    public DbSet<Flat> Flats => Set<Flat>();

    public DbSet<User> Users => Set<User>();

    public DbSet<ResidentProfile> ResidentProfiles => Set<ResidentProfile>();

    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<BusinessType> BusinessTypes => Set<BusinessType>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<BusinessPhoto> BusinessPhotos => Set<BusinessPhoto>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusChange> OrderStatusChanges => Set<OrderStatusChange>();

    // Read by the query filter on every query, so the filter follows this context's society.
    private Guid? CurrentSocietyId => currentSociety.SocietyId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Ids come from Entity (Guid v7), never the database. Saying so lets EF add new child rows
            // reached through a loaded parent (an order's history) instead of treating them as updates.
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
            {
                ConfigureTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Times are stored in UTC; mark them as UTC when read back.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();

        // Rupees with paise; the same precision on every provider.
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    public void DiscardPendingChanges()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenancyRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenancyRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static readonly System.Reflection.MethodInfo ConfigureTenantMethod =
        typeof(AppDbContext).GetMethod(nameof(ConfigureTenant), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private void ConfigureTenant<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasQueryFilter(TenancyFilter, e => e.SocietyId == CurrentSocietyId);
        entity.HasIndex(e => e.SocietyId);
        entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// New tenant rows get the current society; a row can never be written into another
    /// society or moved between societies. Without a current society (sign-up, platform
    /// admin, seeding) the caller must set SocietyId explicitly.
    /// </summary>
    private void ApplyTenancyRules()
    {
        var current = CurrentSocietyId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.SocietyId == Guid.Empty)
                    {
                        entry.Entity.SocietyId = current
                            ?? throw new InvalidOperationException(
                                $"{entry.Entity.GetType().Name} needs a SocietyId and there is no current society.");
                    }
                    else if (current is not null && entry.Entity.SocietyId != current)
                    {
                        throw CrossSocietyWrite(entry.Entity);
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var property = entry.Property(e => e.SocietyId);
                    if (property.IsModified && property.OriginalValue != property.CurrentValue)
                    {
                        throw new InvalidOperationException(
                            $"{entry.Entity.GetType().Name} cannot be moved to another society.");
                    }

                    if (current is not null && property.OriginalValue != current)
                    {
                        throw CrossSocietyWrite(entry.Entity);
                    }

                    break;
            }
        }
    }

    private static InvalidOperationException CrossSocietyWrite(ITenantEntity entity) =>
        new($"{entity.GetType().Name} belongs to another society.");
}
