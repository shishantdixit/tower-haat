using TowerHaat.Api.Auth;
using TowerHaat.Application.Tenancy;

namespace TowerHaat.Api.Tenancy;

/// <summary>Sets the request's society from the signed-in user's token (after authentication).</summary>
internal static class SocietyContextMiddleware
{
    public static IApplicationBuilder UseSocietyContext(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var user = context.User;
            context.RequestServices.GetRequiredService<SocietyContext>()
                .SetHome(user.GetSocietyId(), canUseAnySociety: user.IsInRole(AppClaims.PlatformAdminRole));
            return next(context);
        });
}
