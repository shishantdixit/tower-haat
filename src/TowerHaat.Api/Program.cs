using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TowerHaat.Api.Admin;
using TowerHaat.Api.Auth;
using TowerHaat.Api.Errors;
using TowerHaat.Api.Health;
using TowerHaat.Api.Residents;
using TowerHaat.Api.Societies;
using TowerHaat.Api.Tenancy;
using TowerHaat.Application;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Auth;
using TowerHaat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.Section))
    .Validate(o => o.Otp.HashKey.Length >= 32, "Auth:Otp:HashKey must be at least 32 characters.")
    .PostConfigure(o =>
    {
        // Showing the code is a stand-in for SMS during development only.
        o.Otp.ExposeCodeInResponse &= builder.Environment.IsDevelopment();
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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppClaims.PlatformAdminPolicy, policy => policy.RequireRole(AppClaims.PlatformAdminRole));

// Per-IP limit on sign-in endpoints, on top of the per-phone OTP limits in AuthService.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The Angular build is copied into wwwroot (see web/angular.json).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseSocietyContext();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapSocietyEndpoints();
app.MapAdminSocietyEndpoints();

// Angular routes get index.html; files (nonfile) and unknown /api routes stay 404.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api(/|$)))}", "index.html");

app.Run();

public partial class Program;
