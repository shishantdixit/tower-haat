using Microsoft.Extensions.Options;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Api.Jobs;

/// <summary>Makes a database backup when the newest one is more than a day old, checking hourly while the app runs.</summary>
internal sealed partial class BackupWorker(
    IDatabaseBackup backups, IOptions<JobOptions> options, TimeProvider time, ILogger<BackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunInProcess)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try
            {
                var newest = backups.List().FirstOrDefault();
                if (newest is null || newest.CreatedAt < time.GetUtcNow().UtcDateTime.AddHours(-23))
                {
                    var made = await backups.CreateAsync(stoppingToken);
                    LogMade(made.Name, made.SizeBytes);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database backup {Name} written ({Bytes} bytes)")]
    private partial void LogMade(string name, long bytes);

    [LoggerMessage(Level = LogLevel.Error, Message = "Database backup failed")]
    private partial void LogFailed(Exception exception);
}
