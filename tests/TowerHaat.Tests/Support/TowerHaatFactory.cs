using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Notifications;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Tests.Support;

/// <summary>The real API over SQLite in memory, with SMS captured instead of sent.</summary>
public sealed class TowerHaatFactory : WebApplicationFactory<Program>
{
    // One admin phone per test that needs one, so OTP cooldowns do not collide.
    public const string AdminPhone = "+919999900000";
    public const string SecondAdminPhone = "+919999900001";
    public const string JobKey = "test-job-key-0123456789abcdef0123456789";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public string LogDirectory { get; } = Path.Combine(Path.GetTempPath(), "towerhaat-tests", Guid.NewGuid().ToString("N"));

    public CapturingSmsSender Sms { get; } = new();

    public InMemoryFileStorage Files { get; } = new();

    public CapturingPushSender Push { get; } = new();

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "unused; replaced by SQLite");
        builder.UseSetting("Jwt:SigningKey", new string('j', 32));
        builder.UseSetting("Auth:Otp:HashKey", new string('o', 32));
        builder.UseSetting("Auth:Otp:ExposeCodeInResponse", "true");
        builder.UseSetting("Auth:AdminPhones:0", "99999 00000");
        builder.UseSetting("Auth:AdminPhones:1", SecondAdminPhone);
        builder.UseSetting("Jobs:Key", JobKey);
        builder.UseSetting("Jobs:RunInProcess", "false");
        builder.UseSetting("Logs:Path", LogDirectory);
        builder.UseSetting("Backups:Path", Path.Combine(LogDirectory, "backups"));

        builder.ConfigureTestServices(services =>
        {
            _connection.Open();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender>(Sms);
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(Files);
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
