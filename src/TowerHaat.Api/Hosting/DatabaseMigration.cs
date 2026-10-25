using Microsoft.EntityFrameworkCore;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Api.Hosting;

/// <summary>
/// Applies pending migrations when the app starts, for hosts where running tools against the
/// database is awkward (MonsterASP). EF Core takes a lock, so two starting instances cannot both migrate.
/// </summary>
internal static class DatabaseMigration
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}
