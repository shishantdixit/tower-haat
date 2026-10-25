using Microsoft.Extensions.DependencyInjection;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Admin;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Directory;
using TowerHaat.Application.Notifications;
using TowerHaat.Application.Orders;
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
        services.AddScoped<BusinessTypeService>();
        services.AddScoped<OwnerBusinessService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<BusinessAdminService>();
        services.AddScoped<DirectoryService>();
        services.AddScoped<OrderNotifications>();
        services.AddScoped<PushSubscriptionService>();
        services.AddScoped<OrderViews>();
        services.AddScoped<OrderService>();
        services.AddScoped<OwnerOrderService>();
        services.AddSingleton<OrderExpiryJob>();
        services.AddScoped<PilotStatsService>();

        return services;
    }
}
