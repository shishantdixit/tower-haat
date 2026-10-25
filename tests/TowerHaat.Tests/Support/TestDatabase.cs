using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
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

    public AppDbContext CreateContext(Guid? societyId) => new(_options, new FixedSociety(societyId));

    public void Dispose() => _connection.Dispose();

    private sealed class FixedSociety(Guid? societyId) : ICurrentSociety
    {
        public Guid? SocietyId => societyId;
    }
}
