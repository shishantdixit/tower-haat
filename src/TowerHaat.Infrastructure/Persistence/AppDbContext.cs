using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Domain.Common;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentSociety currentSociety)
    : DbContext(options)
{
    public const string TenancyFilter = "Tenancy";

    public DbSet<Society> Societies => Set<Society>();

    public DbSet<Tower> Towers => Set<Tower>();

    // Read by the query filter on every query, so the filter follows this context's society.
    private Guid? CurrentSocietyId => currentSociety.SocietyId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
            {
                ConfigureTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
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
