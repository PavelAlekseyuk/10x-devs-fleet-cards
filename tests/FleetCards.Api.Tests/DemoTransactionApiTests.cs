using System.Net;
using System.Net.Http.Json;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetCards.Api.Tests;

public sealed class DemoTransactionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public DemoTransactionApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Generation_requires_readwrite_and_csrf_and_audits_success()
    {
        using var host = CreateHost();
        using var anonymous = host.CreateClient();
        using var anonymousResponse = await anonymous.PostAsJsonAsync(
            "/api/admin/demo-transactions",
            ValidRequest()
        );
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var readOnly = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");
        using var deniedResponse = await readOnly.PostAsJsonAsync(
            "/api/admin/demo-transactions",
            ValidRequest()
        );
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);

        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        var csrf = client.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var missingCsrfResponse = await client.PostAsJsonAsync(
            "/api/admin/demo-transactions",
            ValidRequest()
        );
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrfResponse.StatusCode);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf);

        using var response = await client.PostAsJsonAsync(
            "/api/admin/demo-transactions",
            ValidRequest()
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result =
            (await response.Content.ReadFromJsonAsync<DemoTransactionGenerationResponse>())!;
        Assert.Equal(8, result.GeneratedCount);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var generated = await dbContext
            .Transactions.AsNoTracking()
            .Where(transaction =>
                transaction.OccurredAt >= new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)
                && transaction.OccurredAt < new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)
                && transaction.Id
                    != Guid.Parse("44444444-4444-4444-4444-444444444444")
            )
            .ToArrayAsync();
        Assert.Equal(8, generated.Length);
        var audit = await dbContext
            .AuditEvents.AsNoTracking()
            .SingleAsync(entry => entry.Action == "DemoTransactionsGenerated");
        Assert.Equal("anna.nowak", audit.Actor);
        Assert.Equal("FuelTransactionBatch", audit.EntityType);
        Assert.True(audit.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));

        var resolver = new ProgramResolutionService(new EfProgramResolutionDataSource(dbContext));
        foreach (var transaction in generated)
        {
            var percent = resolver.GetPercent(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                transaction.FuelType,
                transaction.OccurredAt
            );
            Assert.Equal(
                decimal.Round(transaction.UnitPrice * percent, 4, MidpointRounding.AwayFromZero),
                transaction.Discount
            );
        }
    }

    [Theory]
    [InlineData(null, "2026-09-10T00:00:00Z", 8)]
    [InlineData("2026-09-10T00:00:00Z", null, 8)]
    [InlineData("2026-09-10T00:00:00Z", "2026-09-09T00:00:00Z", 8)]
    [InlineData("2026-09-09T00:00:00Z", "2026-09-10T00:00:00Z", 0)]
    [InlineData("2026-09-09T00:00:00Z", "2026-09-10T00:00:00Z", 1001)]
    public async Task Generation_rejects_invalid_periods_and_counts(
        string? from,
        string? to,
        int count
    )
    {
        using var host = CreateHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        using var response = await client.PostAsJsonAsync(
            "/api/admin/demo-transactions",
            new { from, to, count }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateHost()
    {
        var databaseName = $"fleet-demo-{Guid.NewGuid()}";
        var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:fleetcards"] = string.Empty,
                        }
                    )
            );
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFleetCardsReadService>();
                services.RemoveAll<IFleetCardService>();
                services.RemoveAll<IDemoTransactionService>();
                services.RemoveAll<IFleetVehicleService>();
                services.AddDbContext<FleetCardsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
                        )
                );
                services.AddScoped<IFleetVehicleService, EfFleetVehicleService>();
                services.AddScoped<EfFleetCardService>();
                services.AddScoped<IFleetCardService>(serviceProvider =>
                    serviceProvider.GetRequiredService<EfFleetCardService>()
                );
                services.AddScoped<IDemoTransactionService>(serviceProvider =>
                    serviceProvider.GetRequiredService<EfFleetCardService>()
                );
                services.AddScoped<IFleetCardsReadService, EfFleetCardsReadService>();
            });
        });

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>().Database.EnsureCreated();
        return host;
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

    private static object ValidRequest() =>
        new
        {
            from = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero),
            to = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
            count = 8,
        };
}
