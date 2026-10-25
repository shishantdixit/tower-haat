namespace TowerHaat.Application.Abstractions;

public sealed record BackupFile(string Name, long SizeBytes, DateTime CreatedAt);

/// <summary>
/// Nightly export of every table to a zip on the server, kept for two weeks and downloadable by
/// the platform admin. A safety net next to the hosting provider's own database backups.
/// </summary>
public interface IDatabaseBackup
{
    Task<BackupFile> CreateAsync(CancellationToken cancellationToken);

    IReadOnlyList<BackupFile> List();

    /// <summary>Opens a backup by name, or null for an unknown or invalid name.</summary>
    Stream? Open(string name);
}
