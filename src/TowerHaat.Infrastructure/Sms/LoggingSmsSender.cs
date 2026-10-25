using Microsoft.Extensions.Logging;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Infrastructure.Sms;

/// <summary>Writes the code to the log instead of sending an SMS. Used until a DLT-registered provider is set up.</summary>
internal sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken)
    {
        LogOtp(phone, code);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS is not configured. OTP for {Phone} is {Code}")]
    private partial void LogOtp(string phone, string code);
}
