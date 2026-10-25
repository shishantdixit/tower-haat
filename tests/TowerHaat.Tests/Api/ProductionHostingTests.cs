using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

public sealed class ProductionHostingTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/health", _ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/index.html", "no-cache")]
    [InlineData("/ngsw-worker.js", "no-cache")]
    [InlineData("/ngsw.json", "no-cache")]
    [InlineData("/manifest.webmanifest", "no-cache")]
    [InlineData("/main-JPRHA27N.js", "public, max-age=31536000, immutable")]
    [InlineData("/chunk-BDZSi8mb.js", "public, max-age=31536000, immutable")]
    [InlineData("/styles-TFXOPEMF.css", "public, max-age=31536000, immutable")]
    [InlineData("/icons/icon-192x192.png", "public, max-age=86400")]
    [InlineData("/favicon.ico", "public, max-age=86400")]
    public void Angular_build_is_cached_so_new_versions_reach_phones(string path, string expected)
    {
        Assert.Equal(expected, TowerHaat.Api.Hosting.StaticFileCaching.For(path));
    }

    [Fact]
    public async Task Errors_are_logged_to_a_file_the_admin_can_read_and_expected_failures_are_not()
    {
        var client = factory.CreateHttpsClient();
        await factory.SignInAsync(client, TowerHaatFactory.AdminPhone, _ct);

        // A validation failure is an answer to the user, not an error.
        await client.PostAsJsonAsync("/api/auth/otp", new { phone = "123" }, _ct);
        factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Test").LogError("Something broke for real");

        var lines = await WaitForErrorLinesAsync(client);

        Assert.Contains(lines, l => l.Contains("Something broke for real"));
        Assert.DoesNotContain(lines, l => l.Contains("10-digit"));
    }

    [Fact]
    public async Task Error_log_is_for_admins_only()
    {
        var client = factory.CreateHttpsClient();
        await factory.SignInAsync(client, "+919866600001", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/logs/errors", _ct)).StatusCode);
    }

    private async Task<List<string>> WaitForErrorLinesAsync(HttpClient client)
    {
        for (var i = 0; ; i++)
        {
            var body = await client.GetFromJsonAsync<ErrorLines>("/api/admin/logs/errors", _ct);
            if (body!.Lines.Any(l => l.Contains("Something broke")) || i == 40)
            {
                return body.Lines;
            }

            await Task.Delay(50, _ct);
        }
    }

    private sealed record ErrorLines(List<string> Lines);
}
