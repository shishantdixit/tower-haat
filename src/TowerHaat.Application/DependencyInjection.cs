using Microsoft.Extensions.DependencyInjection;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Residents;
using TowerHaat.Application.Societies;
using TowerHaat.Application.Tenancy;

namespace TowerHaat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<SocietyContext>();
        services.AddScoped<ICurrentSociety>(sp => sp.GetRequiredService<SocietyContext>());
        services.AddScoped<ISocietyScope>(sp => sp.GetRequiredService<SocietyContext>());

        services.AddScoped<AuthService>();
        services.AddScoped<MeService>();
        services.AddScoped<SocietyDirectoryService>();
        services.AddScoped<SocietyAdminService>();

        return services;
    }
}
