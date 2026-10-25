using Microsoft.EntityFrameworkCore;
using TowerHaat.Domain.Auth;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Notifications;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Domain.Users;

namespace TowerHaat.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Society> Societies { get; }

    DbSet<Tower> Towers { get; }

    DbSet<Flat> Flats { get; }

    DbSet<User> Users { get; }

    DbSet<ResidentProfile> ResidentProfiles { get; }

    DbSet<OtpRequest> OtpRequests { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<BusinessType> BusinessTypes { get; }

    DbSet<Category> Categories { get; }

    DbSet<Business> Businesses { get; }

    DbSet<BusinessPhoto> BusinessPhotos { get; }

    DbSet<CatalogItem> CatalogItems { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<OrderStatusChange> OrderStatusChanges { get; }

    DbSet<Payment> Payments { get; }

    DbSet<PushSubscription> PushSubscriptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Forgets unsaved new rows, so a failed save can be retried cleanly.</summary>
    void DiscardPendingChanges();
}
