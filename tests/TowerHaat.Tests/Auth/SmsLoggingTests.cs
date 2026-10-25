using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Tests.Auth;

/// <summary>Sign-in codes never land in logs outside development.</summary>
public sealed class SmsLoggingTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public async Task Code_is_logged_only_in_development(string environment, bool logged)
    {
        var logs = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var type = typeof(TowerHaat.Infrastructure.DependencyInjection).Assembly.GetType("TowerHaat.Infrastructure.Sms.LoggingSmsSender")!;
        var logger = Activator.CreateInstance(typeof(Logger<>).MakeGenericType(type), factory)!;
        var sender = (ISmsSender)Activator.CreateInstance(type, logger, new Environment(environment))!;

        await sender.SendOtpAsync("+919876543210", "482913", TestContext.Current.CancellationToken);

        var line = Assert.Single(logs.Messages);
        Assert.Equal(logged, line.Contains("482913"));
        Assert.Equal(logged, line.Contains("+919876543210"));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "TowerHaat";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(Messages);

        public void Dispose()
        {
        }

        private sealed class Logger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }
    }
}
