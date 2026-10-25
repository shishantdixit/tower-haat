using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Tenancy;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Tests.Support;

/// <summary>
/// A real relational database (SQLite in memory) shared by several contexts, so tests
/// exercise the same query translation as SQL Server rather than LINQ to Objects.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = CreateContext(societyId: null);
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext(Guid? societyId, bool canUseAnySociety = false) =>
        CreateContext(Society(societyId, canUseAnySociety));

    public AppDbContext CreateContext(SocietyContext society) => new(_options, society);

    public static SocietyContext Society(Guid? societyId, bool canUseAnySociety = false)
    {
        var society = new SocietyContext();
        society.SetHome(societyId, canUseAnySociety);
        return society;
    }

    public void Dispose() => _connection.Dispose();
}
