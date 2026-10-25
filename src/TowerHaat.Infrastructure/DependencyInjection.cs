using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TowerHaat.Application.Abstractions;
using TowerHaat.Infrastructure.Persistence;
using TowerHaat.Infrastructure.Sms;

namespace TowerHaat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Replaced by a real provider once the DLT sender and template are approved.
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        return services;
    }
}
