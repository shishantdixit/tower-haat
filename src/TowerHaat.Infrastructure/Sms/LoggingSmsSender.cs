using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Infrastructure.Sms;

/// <summary>
/// Stand-in until a DLT-registered SMS provider is set up: nothing is sent. In Development the
/// code is written to the log; anywhere else only the failure is, because logs are kept for weeks
/// and codes are secrets. (Outside production the sign-in screen shows the code instead.)
/// </summary>
internal sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger, IHostEnvironment environment) : ISmsSender
{
    public Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            LogOtp(phone, code);
        }
        else
        {
            LogNotSent(phone[..^4] + "XXXX");
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS is not configured. OTP for {Phone} is {Code}")]
    private partial void LogOtp(string phone, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "SMS is not configured: no sign-in code was sent to {Phone}")]
    private partial void LogNotSent(string phone);
}
