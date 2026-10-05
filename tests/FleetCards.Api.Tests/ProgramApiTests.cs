using System.Net;
using System.Net.Http.Json;
using FleetCards.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FleetCards.Api.Tests;

public sealed class ProgramApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ComparisonUri =
        "/api/programs/comparison?from=2026-09-01T00%3A00%3A00Z&to=2026-10-01T00%3A00%3A00Z";
    private readonly WebApplicationFactory<Program> factory;

    public ProgramApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Program_comparison_requires_ReadWrite_and_returns_report_baseline()
    {
        using var anonymousClient = factory.CreateClient();
        using var anonymousResponse = await anonymousClient.GetAsync(ComparisonUri);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var readOnlyClient = await SignInAsync(factory, "piotr.wisniewski", "Piotr123!");
        using var deniedResponse = await readOnlyClient.GetAsync(ComparisonUri);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);

        using var readWriteClient = await SignInAsync(factory, "anna.nowak", "Anna123!");
        using var response = await readWriteClient.GetAsync(ComparisonUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comparison = (await response.Content.ReadFromJsonAsync<ProgramComparisonResponse>())!;
        Assert.Equal(
            new ProgramComparisonBaseline(265.53m, 61.07m, 326.60m),
            comparison.Baseline
        );
        Assert.Equal(4, comparison.Programs.Count);
        Assert.Equal(
            comparison.Programs
                .OrderBy(program => program.HypotheticalGrossTotal)
                .ThenBy(program => program.ProgramName, StringComparer.Ordinal)
                .Select(program => program.ProgramId),
            comparison.Programs.Select(program => program.ProgramId)
        );
    }

    private static async Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> host,
        string userName,
        string password
    )
    {
        var client = host.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        using var csrfResponse = await client.GetAsync("/api/auth/csrf");
        var setCookie = csrfResponse.Headers.GetValues("Set-Cookie").Single();
        var token = setCookie[..setCookie.IndexOf(';')].Split('=', 2)[1];
        client.DefaultRequestHeaders.Add("Cookie", $"XSRF-TOKEN={token}");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);

        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(userName, password)
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
