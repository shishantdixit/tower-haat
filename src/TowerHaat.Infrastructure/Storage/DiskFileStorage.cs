using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string Section = "Storage";

    /// <summary>Folder for uploads; relative paths are under the app's content root.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
}

/// <summary>Stores uploads on the host's disk as yyyy/MM/{guid}.ext. Keys are checked so they never leave the root.</summary>
internal sealed partial class DiskFileStorage(IOptions<FileStorageOptions> options, TimeProvider time) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = PathFor(key) ?? throw new ArgumentException("Unsupported file type.", nameof(extension));

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Stream? OpenRead(string key)
    {
        var path = PathFor(key);
        return path is not null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string? PathFor(string key) =>
        KeyPattern().IsMatch(key) ? Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)) : null;

    [GeneratedRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$")]
    private static partial Regex KeyPattern();
}
