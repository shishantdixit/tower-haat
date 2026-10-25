using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TowerHaat.Application.Abstractions;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

/// <summary>Daily database backup: every table exported, admin-only download, safe file names.</summary>
public sealed class BackupTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Backup_contains_every_table_with_its_rows_and_a_manifest()
    {
        var admin = factory.CreateHttpsClient();
        await factory.SignUpAsync(admin, TowerHaatFactory.AdminPhone, "Admin", _ct);

        var made = await (await admin.PostAsync("/api/admin/backups", null, _ct)).Content.ReadFromJsonAsync<BackupFile>(_ct);
        var download = await admin.GetAsync($"/api/admin/backups/{made!.Name}", _ct);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        using var zip = new ZipArchive(await download.Content.ReadAsStreamAsync(_ct));
        Assert.Contains(zip.Entries, e => e.Name == "Orders.jsonl");
        Assert.Contains(zip.Entries, e => e.Name == "PushSubscriptions.jsonl");

        var societies = await ReadLinesAsync(zip, "Societies.jsonl");
        Assert.Contains(societies, l => l.Contains("\"Name\":\"Ace Divino\""));
        var users = await ReadLinesAsync(zip, "Users.jsonl");
        Assert.Contains(users, l => l.Contains(TowerHaatFactory.AdminPhone.Replace("+", "\\u002B")) || l.Contains(TowerHaatFactory.AdminPhone));

        using var manifest = JsonDocument.Parse(await ReadAllAsync(zip, "manifest.json"));
        Assert.EndsWith("AddPushNotifications", manifest.RootElement.GetProperty("latestMigration").GetString() ?? "AddPushNotifications");
        Assert.Equal(1, manifest.RootElement.GetProperty("rows").GetProperty("Societies").GetInt32());

        var listed = await admin.GetFromJsonAsync<List<BackupFile>>("/api/admin/backups", _ct);
        Assert.Contains(listed!, b => b.Name == made.Name && b.SizeBytes > 0);
    }

    [Fact]
    public async Task Only_backup_names_can_be_downloaded()
    {
        var admin = factory.CreateHttpsClient();
        await factory.SignInAsync(admin, TowerHaatFactory.SecondAdminPhone, _ct);

        foreach (var name in new[] { "..%2F..%2Fappsettings.json", "towerhaat-backup-x.zip", "secrets.zip" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/backups/{name}", _ct)).StatusCode);
        }
    }

    [Fact]
    public async Task Residents_cannot_list_or_trigger_backups()
    {
        var client = factory.CreateHttpsClient();
        await factory.SignInAsync(client, "+919877700001", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/backups", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/jobs/backup", null, _ct)).StatusCode);
    }

    private static async Task<List<string>> ReadLinesAsync(ZipArchive zip, string name) =>
        (await ReadAllAsync(zip, name)).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static async Task<string> ReadAllAsync(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return await reader.ReadToEndAsync();
    }
}
