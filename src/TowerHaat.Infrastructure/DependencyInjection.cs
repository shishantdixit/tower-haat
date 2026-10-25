using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Notifications;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Infrastructure.Push;
using TowerHaat.Infrastructure.Sms;
using TowerHaat.Infrastructure.Storage;

namespace TowerHaat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Replaced by a real provider once the DLT sender and template are approved.
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.Section))
            .PostConfigure(o => o.RootPath = Path.Combine(environment.ContentRootPath, o.RootPath));
        services.AddSingleton<IFileStorage, DiskFileStorage>();

        services.AddOptions<PushOptions>().Bind(configuration.GetSection(PushOptions.Section));
        services.AddHttpClient<IPushSender, WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
