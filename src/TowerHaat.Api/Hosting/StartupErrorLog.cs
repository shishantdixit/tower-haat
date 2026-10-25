namespace TowerHaat.Api.Hosting;

/// <summary>
/// Writes an exception that stops the app from starting (missing settings, database unreachable) to
/// App_Data/logs/startup-error.log. It happens before the file logs exist, and on IIS the browser
/// only shows "500", so this file is the only place the reason can be read.
/// </summary>
internal static class StartupErrorLog
{
    public static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var folder = Path.Combine(AppContext.BaseDirectory, "App_Data", "logs");
                System.IO.Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, "startup-error.log"),
                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}{e.ExceptionObject}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Nothing else can record it.
            }
        };
    }
}
