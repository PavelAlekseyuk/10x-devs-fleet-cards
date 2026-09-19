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
    public async Task Root_returns_api_information()
    {
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fleet Cards API", await response.Content.ReadAsStringAsync());
    }
}
