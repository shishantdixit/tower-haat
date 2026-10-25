using Microsoft.EntityFrameworkCore;
using TowerHaat.Domain.Auth;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
