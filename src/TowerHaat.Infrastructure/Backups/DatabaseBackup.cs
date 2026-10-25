using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Abstractions;
using TowerHaat.Infrastructure.Persistence;

namespace TowerHaat.Infrastructure.Backups;

public sealed class BackupOptions
{
    public const string Section = "Backups";

    /// <summary>Folder for backup zips; relative paths are under the content root.</summary>
    public string Path { get; set; } = "App_Data/backups";

    public int KeepDays { get; set; } = 14;
}

/// <summary>
/// Writes every table of the model to towerhaat-backup-yyyyMMdd-HHmm.zip as one JSON object per row
/// per line, with a manifest of the latest migration and row counts. Plain SELECTs through the
/// provider's own quoting, so it works on SQL Server and PostgreSQL alike. It reads every society:
/// a system job, outside the tenancy filter by nature, and only the platform admin can download it.
/// </summary>
internal sealed partial class DatabaseBackup(IServiceScopeFactory scopes, IOptions<BackupOptions> options, TimeProvider time) : IDatabaseBackup
{
    private readonly string _directory = options.Value.Path;

    public async Task<BackupFile> CreateAsync(CancellationToken cancellationToken)
    {
        System.IO.Directory.CreateDirectory(_directory);
        var now = time.GetUtcNow().UtcDateTime;
        var name = $"towerhaat-backup-{now:yyyyMMdd-HHmmss}.zip";
        var path = System.IO.Path.Combine(_directory, name);
        var partial = path + ".partial";

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await using var zip = new ZipArchive(file, ZipArchiveMode.Create);
            await WriteAsync(db, zip, now, cancellationToken);
        }

        // Only complete backups get the .zip name, so a crash never leaves a half-written one to restore from.
        File.Move(partial, path);
        DeleteOld(now);
        return Describe(new FileInfo(path));
    }

    public IReadOnlyList<BackupFile> List() =>
        System.IO.Directory.Exists(_directory)
            ? new DirectoryInfo(_directory).GetFiles("towerhaat-backup-*.zip")
                .Where(f => NamePattern().IsMatch(f.Name))
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .Select(Describe)
                .ToList()
            : [];

    public Stream? Open(string name)
    {
        if (!NamePattern().IsMatch(name))
        {
            return null;
        }

        var path = System.IO.Path.Combine(_directory, name);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    private static async Task WriteAsync(AppDbContext db, ZipArchive zip, DateTime now, CancellationToken cancellationToken)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var tables = db.Model.GetEntityTypes()
                .Where(t => t.GetTableName() is not null && !t.IsOwned())
                .Select(t => (Name: t.GetTableName()!, Schema: t.GetSchema()))
                .Distinct()
                .OrderBy(t => t.Name, StringComparer.Ordinal);

            foreach (var (table, schema) in tables)
            {
                var entry = zip.CreateEntry($"{table}.jsonl", CompressionLevel.Optimal);
                await using var stream = entry.Open();
                counts[table] = await WriteTableAsync(connection, sql.DelimitIdentifier(table, schema), stream, cancellationToken);
            }

            var manifest = zip.CreateEntry("manifest.json");
            await using var manifestStream = manifest.Open();
            await JsonSerializer.SerializeAsync(manifestStream, new
            {
                createdAtUtc = now,
                latestMigration = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault(),
                provider = db.Database.ProviderName,
                rows = counts,
            }, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> WriteTableAsync(DbConnection connection, string table, Stream output, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await using var json = new Utf8JsonWriter(output);
        var rows = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            json.WriteStartObject();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                json.WritePropertyName(reader.GetName(i));
                WriteValue(json, reader.IsDBNull(i) ? null : reader.GetValue(i));
            }

            json.WriteEndObject();
            await json.FlushAsync(cancellationToken);
            await output.WriteAsync("\n"u8.ToArray(), cancellationToken);
            json.Reset();
            rows++;
        }

        return rows;
    }

    private static void WriteValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                break;
            case bool b:
                json.WriteBooleanValue(b);
                break;
            case int or long or short or byte:
                json.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case decimal d:
                json.WriteNumberValue(d);
                break;
            case double d:
                json.WriteNumberValue(d);
                break;
            case DateTime dt:
                json.WriteStringValue(DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
                break;
            case byte[] bytes:
                json.WriteBase64StringValue(bytes);
                break;
            default:
                json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private void DeleteOld(DateTime now)
    {
        var cutOff = now.AddDays(-options.Value.KeepDays);
        foreach (var file in new DirectoryInfo(_directory).GetFiles("towerhaat-backup-*"))
        {
            if (file.LastWriteTimeUtc < cutOff)
            {
                file.Delete();
            }
        }
    }

    private static BackupFile Describe(FileInfo file) => new(file.Name, file.Length, file.LastWriteTimeUtc);

    [GeneratedRegex(@"^towerhaat-backup-\d{8}-\d{6}\.zip$")]
    private static partial Regex NamePattern();
}
