using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FleetCards.Api.Tests;

public sealed class PlatformTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public PlatformTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_root_returns_api_information()
    {
        using var response = await client.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fleet Cards API", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/cards/123")]
    public async Task Spa_routes_return_the_web_application(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Fleet Cards", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/not-found")]
    [InlineData("/health/not-found")]
    [InlineData("/missing.js")]
    public async Task Api_health_and_asset_paths_do_not_fall_back_to_the_spa(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
