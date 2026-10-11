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
            new ProgramComparisonBaseline(273.11m, 62.82m, 335.93m),
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
        var nebulaBasic = Assert.Single(
            comparison.Programs,
            program => program.ProgramName == "Nebula Basic"
        );
        Assert.Equal(comparison.Baseline.NetTotal, nebulaBasic.HypotheticalNetTotal);
        Assert.Equal(comparison.Baseline.GrossTotal, nebulaBasic.HypotheticalGrossTotal);
        Assert.Equal(0m, nebulaBasic.NetSavings);
        Assert.Equal(0m, nebulaBasic.GrossSavings);
    }

    [Fact]
    public async Task Program_catalog_and_history_require_ReadWrite_and_include_rules_and_current_assignment()
    {
        using var host = CreateProgramHost();
        using var anonymousClient = host.CreateClient();
        using var anonymousPrograms = await anonymousClient.GetAsync("/api/programs");
        using var anonymousHistory = await anonymousClient.GetAsync("/api/programs/assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPrograms.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHistory.StatusCode);

        using var readOnlyClient = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");
        using var deniedPrograms = await readOnlyClient.GetAsync("/api/programs");
        using var deniedHistory = await readOnlyClient.GetAsync("/api/programs/assignments");
        Assert.Equal(HttpStatusCode.Forbidden, deniedPrograms.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedHistory.StatusCode);

        using var readWriteClient = await SignInAsync(host, "anna.nowak", "Anna123!");
        using var programsResponse = await readWriteClient.GetAsync("/api/programs");
        using var historyResponse = await readWriteClient.GetAsync("/api/programs/assignments");

        Assert.Equal(HttpStatusCode.OK, programsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var catalog = (await programsResponse.Content.ReadFromJsonAsync<FleetCardProgramsResponse>())!;
        Assert.Equal(4, catalog.Programs.Count);
        Assert.All(catalog.Programs, program => Assert.Equal(6, program.Rules!.Count));
        Assert.NotNull(catalog.CurrentAssignment);
        var orionDiesel = Assert.Single(
            catalog.Programs,
            program => program.Id == Guid.Parse("55555555-5555-5555-5555-555555555555")
        );
        var nebulaBasic = Assert.Single(
            catalog.Programs,
            program => program.Id == Guid.Parse("55555555-5555-5555-5555-555555555556")
        );
        Assert.Equal("Orion Diesel", orionDiesel.Name);
        Assert.Equal("Nebula Basic", nebulaBasic.Name);
        Assert.Equal(
            0.10m,
            Assert.Single(orionDiesel.Rules!, rule => rule.FuelType == "Diesel").Percent
        );
        Assert.Equal(
            -0.02m,
            Assert.Single(orionDiesel.Rules!, rule => rule.FuelType == "95").Percent
        );
        Assert.All(nebulaBasic.Rules!, rule => Assert.Equal(0m, rule.Percent));
        Assert.Equal(nebulaBasic.Id, catalog.CurrentAssignment.ProgramId);
        var history = (
            await historyResponse.Content.ReadFromJsonAsync<FleetCardProgramAssignmentResponse[]>()
        )!;
        Assert.Equal(catalog.CurrentAssignment, Assert.Single(history));
    }

    [Fact]
    public async Task Assignment_mutations_enforce_authorization_csrf_and_future_validation()
    {
        using var host = CreateProgramHost();
        using var anonymousClient = host.CreateClient();
        using var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/programs/assignments",
            new { }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var readOnlyClient = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");
        using var readOnlyResponse = await readOnlyClient.PostAsJsonAsync(
            "/api/programs/assignments/end",
            new { endsAt = DateTimeOffset.UtcNow.AddDays(1) }
        );
        Assert.Equal(HttpStatusCode.Forbidden, readOnlyResponse.StatusCode);

        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        var csrfToken = client.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var missingCsrf = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new { programId = Guid.NewGuid(), startsAt = DateTimeOffset.UtcNow.AddDays(1) }
        );
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "not-the-cookie-token");
        using var invalidCsrf = await client.PostAsJsonAsync(
            "/api/programs/assignments/end",
            new { endsAt = DateTimeOffset.UtcNow.AddDays(1) }
        );
        Assert.Equal(HttpStatusCode.Forbidden, invalidCsrf.StatusCode);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrfToken);

        using var missingFields = await client.PostAsJsonAsync("/api/programs/assignments", new { });
        using var pastStart = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new
            {
                programId = Guid.Parse("55555555-5555-5555-5555-555555555556"),
                startsAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            }
        );
        using var unknownProgram = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new { programId = Guid.NewGuid(), startsAt = DateTimeOffset.UtcNow.AddDays(1) }
        );
        using var missingEnd = await client.PostAsJsonAsync(
            "/api/programs/assignments/end",
            new { }
        );
        using var pastEnd = await client.PostAsJsonAsync(
            "/api/programs/assignments/end",
            new { endsAt = DateTimeOffset.UtcNow.AddSeconds(-1) }
        );

        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pastStart.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownProgram.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingEnd.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pastEnd.StatusCode);
    }

    [Fact]
    public async Task Scheduling_a_switch_replaces_the_pending_assignment_and_audits_the_change()
    {
        using var host = CreateProgramHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        var firstStart = DateTimeOffset.UtcNow.AddHours(1);
        var replacementStart = DateTimeOffset.UtcNow.AddHours(2);
        var nebulaProgramId = Guid.Parse("55555555-5555-5555-5555-555555555556");
        var orionProgramId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        using var firstSchedule = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new { programId = orionProgramId, startsAt = firstStart }
        );
        using var replacementSchedule = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new { programId = nebulaProgramId, startsAt = replacementStart }
        );

        Assert.Equal(HttpStatusCode.NoContent, firstSchedule.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replacementSchedule.StatusCode);
        using var historyResponse = await client.GetAsync("/api/programs/assignments");
        var history = (
            await historyResponse.Content.ReadFromJsonAsync<FleetCardProgramAssignmentResponse[]>()
        )!;
        Assert.Equal(2, history.Length);
        var active = Assert.Single(history, assignment => assignment.EndsAt == replacementStart);
        Assert.Equal(replacementStart, active.EndsAt);
        var successor = Assert.Single(
            history,
            assignment => assignment.StartsAt == replacementStart
        );
        Assert.Equal(nebulaProgramId, successor.ProgramId);
        Assert.Equal(active.EndsAt, successor.StartsAt);
        Assert.DoesNotContain(history, assignment => assignment.StartsAt == firstStart);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var audits = await dbContext
            .AuditEvents.AsNoTracking()
            .Where(audit => audit.EntityType == "ClientProgramAssignment")
            .ToArrayAsync();
        Assert.Equal(2, audits.Length);
        Assert.All(
            audits,
            audit =>
            {
                Assert.Equal("anna.nowak", audit.Actor);
                Assert.Equal("ProgramAssignmentScheduled", audit.Action);
                Assert.True(audit.Succeeded);
                Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
            }
        );
    }

    [Fact]
    public async Task Ending_an_assignment_replaces_pending_work_and_leaves_an_uncovered_interval()
    {
        using var host = CreateProgramHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        var pendingStart = DateTimeOffset.UtcNow.AddHours(1);
        var endAt = DateTimeOffset.UtcNow.AddHours(2);
        using var schedule = await client.PostAsJsonAsync(
            "/api/programs/assignments",
            new
            {
                programId = Guid.Parse("55555555-5555-5555-5555-555555555556"),
                startsAt = pendingStart,
            }
        );
        using var end = await client.PostAsJsonAsync(
            "/api/programs/assignments/end",
            new { endsAt = endAt }
        );

        Assert.Equal(HttpStatusCode.NoContent, schedule.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, end.StatusCode);
        using var historyResponse = await client.GetAsync("/api/programs/assignments");
        var history = (
            await historyResponse.Content.ReadFromJsonAsync<FleetCardProgramAssignmentResponse[]>()
        )!;
        var onlyAssignment = Assert.Single(history);
        Assert.Equal(endAt, onlyAssignment.EndsAt);
        Assert.DoesNotContain(history, assignment => assignment.StartsAt >= endAt);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var resolution = new ProgramResolutionService(
            new EfProgramResolutionDataSource(dbContext)
        );
        var percentAfterEnd = resolution.GetPercent(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Diesel",
            endAt.AddSeconds(1)
        );
        Assert.Equal(0m, percentAfterEnd);
        var endAudit = await dbContext
            .AuditEvents.AsNoTracking()
            .SingleAsync(audit => audit.Action == "ProgramAssignmentEnded");
        Assert.Equal("anna.nowak", endAudit.Actor);
        Assert.Equal("ClientProgramAssignment", endAudit.EntityType);
        Assert.True(endAudit.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(endAudit.CorrelationId));
    }

    private WebApplicationFactory<Program> CreateProgramHost()
    {
        var databaseName = $"fleet-programs-{Guid.NewGuid()}";
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
                services.RemoveAll<IProgramAssignmentService>();
                services.AddDbContext<FleetCardsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
                        )
                );
                services.AddScoped<IFleetCardsReadService, EfFleetCardsReadService>();
                services.AddScoped<IProgramAssignmentService, EfProgramAssignmentService>();
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
}
