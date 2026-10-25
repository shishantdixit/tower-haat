using Serilog;
using Serilog.Events;

namespace TowerHaat.Api.Hosting;

public sealed class LogOptions
{
    public const string Section = "Logs";

    /// <summary>Folder for log files; relative paths are under the content root (App_Data is not served by IIS).</summary>
    public string Path { get; set; } = "App_Data/logs";

    public static string Directory(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration.GetSection(Section).Get<LogOptions>()?.Path ?? new LogOptions().Path;
        return System.IO.Path.Combine(environment.ContentRootPath, path);
    }
}

/// <summary>
/// Logs go to the console (IIS stdout when enabled) and to daily files: app-*.log keeps two weeks of
/// everything at Information and above, errors-*.log keeps two months of warnings and errors, which
/// the admin screen shows. MonsterASP has no log viewer, so the files are the record.
/// </summary>
internal static class LoggingSetup
{
    public const string ErrorFilePrefix = "errors-";

    public static void AddFileLogging(this WebApplicationBuilder builder)
    {
        var directory = LogOptions.Directory(builder.Configuration, builder.Environment);
        // Each host keeps its own logger rather than setting the global static one (tests run several hosts at once).
        builder.Services.AddSerilog(
            (services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                System.IO.Path.Combine(directory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true)
            .WriteTo.File(
                System.IO.Path.Combine(directory, $"{ErrorFilePrefix}.log"),
                restrictedToMinimumLevel: LogEventLevel.Warning,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 60,
                shared: true),
            preserveStaticLogger: true);
    }
}
