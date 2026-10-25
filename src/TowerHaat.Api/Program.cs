using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TowerHaat.Api.Admin;
using TowerHaat.Api.Auth;
using TowerHaat.Api.Businesses;
using TowerHaat.Api.Directory;
using TowerHaat.Api.Errors;
using TowerHaat.Api.Health;
using TowerHaat.Api.Hosting;
using TowerHaat.Api.Jobs;
using TowerHaat.Api.Media;
using TowerHaat.Api.Notifications;
using TowerHaat.Api.Orders;
using TowerHaat.Api.Residents;
using TowerHaat.Api.Societies;
using TowerHaat.Api.Tenancy;
using TowerHaat.Application;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Notifications;
using TowerHaat.Infrastructure;

if (args is ["generate-vapid-keys"])
{
    PushEndpoints.PrintNewVapidKeys();
    return;
}

StartupErrorLog.Register();

var builder = WebApplication.CreateBuilder(args);
builder.AddFileLogging();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.Section))
    .Validate(o => o.Otp.HashKey.Length >= 32, "Auth:Otp:HashKey must be at least 32 characters.")
    .PostConfigure(o =>
    {
        // Showing the code stands in for SMS in development and staging, never in production.
        o.Otp.ExposeCodeInResponse &= !builder.Environment.IsProduction();
        o.AdminPhones = o.AdminPhones
            .Select(p => TowerHaat.Domain.Users.PhoneNumber.TryNormalize(p, out var n) ? n : p)
            .ToList();
    })
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters.")
    .ValidateOnStart();

builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<ChannelNotificationQueue>();
builder.Services.AddSingleton<INotificationQueue>(sp => sp.GetRequiredService<ChannelNotificationQueue>());
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddOptions<JobOptions>().Bind(builder.Configuration.GetSection(JobOptions.Section));
builder.Services.AddHostedService<OrderExpiryWorker>();
builder.Services.AddHostedService<BackupWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(jwt),
            NameClaimType = AppClaims.UserId,
            RoleClaimType = AppClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // WebSockets cannot send headers, so the hub takes the token from the query string.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(OrdersHub.Path))
                {
                    context.Token = context.Request.Query["access_token"];
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppClaims.PlatformAdminPolicy, policy => policy.RequireRole(AppClaims.PlatformAdminRole))
    .AddPolicy(AppClaims.ResidentPolicy, policy => policy.RequireClaim(AppClaims.SocietyId));

// Per-IP limit on requesting and checking codes, on top of the per-phone OTP limits in AuthService.
// Generous, because flats in a society often share one public IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ExceptionHandlerOptions>(o =>
    // Expected failures (validation, not found…) are answers, not errors; keep them out of the error log.
    o.SuppressDiagnosticsCallback = context => context.Exception is TowerHaat.Application.Common.AppException);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.MigrateDatabaseAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// The Angular build is copied into wwwroot (see web/angular.json).
app.UseDefaultFiles();
app.UseStaticFiles(StaticFileCaching.Options());

app.UseAuthentication();
app.UseSocietyContext();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapSocietyEndpoints();
app.MapAdminSocietyEndpoints();
app.MapAdminLogEndpoints();
app.MapAdminBackupEndpoints();
app.MapBusinessTypeEndpoints();
app.MapOwnerBusinessEndpoints();
app.MapAdminBusinessEndpoints();
app.MapDirectoryEndpoints();
app.MapOrderEndpoints();
app.MapJobEndpoints();
app.MapPushEndpoints();
app.MapHub<OrdersHub>(OrdersHub.Path);
app.MapMediaEndpoints();

// Angular routes get index.html; files (nonfile) and unknown /api, /media and /hubs routes stay 404.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!(api|media|hubs)(/|$)))}", "index.html", StaticFileCaching.Options());

app.Run();

public partial class Program;
