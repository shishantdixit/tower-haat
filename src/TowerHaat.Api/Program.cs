using TowerHaat.Api.Health;
using TowerHaat.Api.Tenancy;
using TowerHaat.Application.Abstractions;
using TowerHaat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentSociety, HttpCurrentSociety>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The Angular build is copied into wwwroot (see web/angular.json).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();

// Angular routes get index.html; files (nonfile) and unknown /api routes stay 404.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api(/|$)))}", "index.html");

app.Run();

public partial class Program;
