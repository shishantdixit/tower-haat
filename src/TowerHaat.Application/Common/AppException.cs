namespace TowerHaat.Application.Common;

public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests,
}

/// <summary>An expected failure whose message is safe to show to the user.</summary>
public sealed class AppException(ErrorKind kind, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
