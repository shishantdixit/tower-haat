using System.Net;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

public sealed class HostingTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/does-not-exist")]
    [InlineData("/missing-file.js")]
    public async Task Unknown_api_routes_and_files_are_not_served_the_angular_app(string path)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
