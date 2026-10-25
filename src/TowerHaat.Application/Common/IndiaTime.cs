namespace TowerHaat.Application.Common;

/// <summary>Times are stored in UTC and shown, and compared with business hours, in Indian time.</summary>
public static class IndiaTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, Zone);

    public static TimeOnly TimeNow(TimeProvider time) => TimeOnly.FromDateTime(Now(time));
}
