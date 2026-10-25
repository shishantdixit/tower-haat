using Microsoft.Extensions.Options;
using TowerHaat.Application.Orders;

namespace TowerHaat.Api.Jobs;

/// <summary>Expires overdue orders every minute while the app is running.</summary>
internal sealed partial class OrderExpiryWorker(
    OrderExpiryJob job, IOptions<JobOptions> options, TimeProvider time, ILogger<OrderExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunInProcess)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var expired = await job.RunAsync(stoppingToken);
                if (expired > 0)
                {
                    LogExpired(expired);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(e);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} unaccepted orders")]
    private partial void LogExpired(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Order expiry failed")]
    private partial void LogFailed(Exception exception);
}
