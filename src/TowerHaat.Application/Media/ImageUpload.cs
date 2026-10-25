using TowerHaat.Application.Common;

namespace TowerHaat.Application.Media;

/// <summary>
/// Photos are resized in the browser before upload; the server only accepts small JPEG, PNG
/// or WebP files and checks their first bytes rather than trusting the declared type.
/// </summary>
public static class ImageUpload
{
    public const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>Reads and checks the upload; returns the content and its file extension.</summary>
    public static async Task<(MemoryStream Content, string Extension)> ReadAsync(
        Stream stream, long length, CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            throw new AppException(ErrorKind.Validation, "Choose a photo.");
        }

        if (length > MaxBytes)
        {
            throw new AppException(ErrorKind.Validation, "The photo is too large. Please use one under 2 MB.");
        }

        var content = new MemoryStream();
        await stream.CopyToAsync(content, cancellationToken);
        content.Position = 0;

        var extension = Detect(content.GetBuffer().AsSpan(0, (int)Math.Min(content.Length, 12)))
            ?? throw new AppException(ErrorKind.Validation, "Upload a JPEG, PNG or WebP photo.");

        return (content, extension);
    }

    public static string? Detect(ReadOnlySpan<byte> head) => head switch
    {
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ".png",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ".webp",
        _ => null,
    };

    public static string ContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    /// <summary>Public URL of a stored photo, served by the media endpoint.</summary>
    public static string? Url(string? key) => key is null ? null : $"/media/{key}";
}
