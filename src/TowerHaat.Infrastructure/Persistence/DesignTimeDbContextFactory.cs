using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TowerHaat.Application.Tenancy;

namespace TowerHaat.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only; migrations never need a society.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=TowerHaat;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options, new SocietyContext());
    }
}
