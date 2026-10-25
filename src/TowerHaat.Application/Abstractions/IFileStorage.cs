namespace TowerHaat.Application.Abstractions;

/// <summary>Where uploaded images live: the host's disk now, S3 or blob storage later.</summary>
public interface IFileStorage
{
    /// <summary>Stores the content under a new unique key and returns the key.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>Opens a stored file, or null when it does not exist.</summary>
    Stream? OpenRead(string key);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
