namespace TowerHaat.Application.Common;

/// <summary>Times are stored in UTC and shown, and compared with business hours, in Indian time.</summary>
public static class IndiaTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, Zone);

    public static TimeOnly TimeNow(TimeProvider time) => TimeOnly.FromDateTime(Now(time));

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time));

    /// <summary>A date and time in India as UTC.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);

    /// <summary>Midnight today in India, as UTC: where "today" starts for daily limits and summaries.</summary>
    public static DateTime StartOfTodayUtc(TimeProvider time) => ToUtc(Today(time), TimeOnly.MinValue);
}
