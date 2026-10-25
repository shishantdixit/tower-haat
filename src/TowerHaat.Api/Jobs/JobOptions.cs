namespace TowerHaat.Api.Jobs;

public sealed class JobOptions
{
    public const string Section = "Jobs";

    /// <summary>
    /// Secret the hosting provider's scheduled task sends in the X-Job-Key header. When empty,
    /// the job endpoints are switched off.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Also run jobs on a timer inside the app, while IIS keeps it loaded.</summary>
    public bool RunInProcess { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
}
