using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FleetCards.Api.Tests;

public sealed class CardApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SeededCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FirstVehicleId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly Guid SecondVehicleId = Guid.Parse(
        "33333333-3333-3333-3333-333333333333"
    );
    private readonly WebApplicationFactory<Program> factory;

    public CardApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Anonymous_card_reads_and_mutations_are_rejected()
    {
        using var host = CreateCardHost();
        using var client = host.CreateClient();

        using var cards = await client.GetAsync("/api/cards");
        using var history = await client.GetAsync($"/api/cards/{SeededCardId}/state-history");
        using var issue = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        using var state = await client.PutAsJsonAsync(
            $"/api/cards/{SeededCardId}/state",
            new ChangeFleetCardStateRequest("Blocked")
        );
        using var assignment = await client.PutAsync($"/api/cards/{SeededCardId}/assignment", null);

        Assert.Equal(HttpStatusCode.Unauthorized, cards.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, history.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, issue.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, state.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assignment.StatusCode);
    }

    [Fact]
    public async Task Read_only_users_can_read_cards_and_history_but_cannot_mutate()
    {
        using var host = CreateCardHost();
        using var client = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");

        using var cards = await client.GetAsync("/api/cards");
        using var history = await client.GetAsync($"/api/cards/{SeededCardId}/state-history");
        using var issue = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        using var state = await client.PutAsJsonAsync(
            $"/api/cards/{SeededCardId}/state",
            new ChangeFleetCardStateRequest("Blocked")
        );
        using var assignment = await client.PutAsync($"/api/cards/{SeededCardId}/assignment", null);

        Assert.Equal(HttpStatusCode.OK, cards.StatusCode);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, issue.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, state.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assignment.StatusCode);
        var historyEntries = (
            await history.Content.ReadFromJsonAsync<FleetCardStateHistoryResponse[]>()
        )!;
        Assert.Equal("Active", Assert.Single(historyEntries).State);
    }

    [Fact]
    public async Task Read_write_users_can_issue_change_state_and_read_newest_first_history()
    {
        using var host = CreateCardHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");

        using var initialCards = await client.GetAsync("/api/cards");
        var initial = (await initialCards.Content.ReadFromJsonAsync<FleetCardApiResponse[]>())!;
        Assert.All(
            initial,
            card =>
            {
                Assert.NotEqual(Guid.Empty, card.AssignedCarId);
                Assert.False(string.IsNullOrWhiteSpace(card.AssignedVehicleRegistrationPlate));
            }
        );
        var seeded = Assert.Single(initial, card => card.Id == SeededCardId);
        Assert.Equal("**** **** **** 4242", seeded.MaskedPan);
        Assert.Equal("mock-card-4242", seeded.ProviderToken);
        Assert.Equal("Active", seeded.State);
        Assert.Equal(FirstVehicleId, seeded.AssignedCarId);
        Assert.Equal("WA 12345", seeded.AssignedVehicleRegistrationPlate);

        using var issue = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        var issued = (await issue.Content.ReadFromJsonAsync<FleetCardApiResponse>())!;
        Assert.Equal($"/api/cards/{issued.Id}", issue.Headers.Location?.ToString());
        Assert.Equal("Active", issued.State);
        Assert.Equal(SecondVehicleId, issued.AssignedCarId);
        Assert.Equal("PO 98765", issued.AssignedVehicleRegistrationPlate);

        using var initialHistoryResponse = await client.GetAsync(
            $"/api/cards/{issued.Id}/state-history"
        );
        Assert.Equal(HttpStatusCode.OK, initialHistoryResponse.StatusCode);
        var initialHistory = (
            await initialHistoryResponse.Content.ReadFromJsonAsync<FleetCardStateHistoryResponse[]>()
        )!;
        var issuance = Assert.Single(initialHistory);
        Assert.Equal("Active", issuance.State);
        Assert.Equal(TimeSpan.Zero, issuance.OccurredAt.Offset);

        foreach (var stateName in new[] { "Blocked", "Stolen", "Archived", "Active" })
        {
            using var stateResponse = await client.PutAsJsonAsync(
                $"/api/cards/{issued.Id}/state",
                new ChangeFleetCardStateRequest(stateName)
            );
            Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
            var updated = (await stateResponse.Content.ReadFromJsonAsync<FleetCardApiResponse>())!;
            Assert.Equal(stateName, updated.State);
            Assert.Equal(SecondVehicleId, updated.AssignedCarId);
        }

        using var assignmentMutation = await client.PutAsync(
            $"/api/cards/{issued.Id}/assignment",
            null
        );
        Assert.Equal(HttpStatusCode.NotFound, assignmentMutation.StatusCode);

        using var historyResponse = await client.GetAsync($"/api/cards/{issued.Id}/state-history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = (
            await historyResponse.Content.ReadFromJsonAsync<FleetCardStateHistoryResponse[]>()
        )!;
        Assert.Equal(
            new[] { "Active", "Archived", "Stolen", "Blocked", "Active" },
            history.Select(entry => entry.State)
        );
        Assert.All(history, entry => Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset));

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var audits = await dbContext
            .AuditEvents.AsNoTracking()
            .Where(audit => audit.EntityId == issued.Id)
            .OrderBy(audit => audit.OccurredAt)
            .ToArrayAsync();
        Assert.Equal(5, audits.Length);
        Assert.Equal(1, audits.Count(audit => audit.Action == "CardIssued"));
        Assert.Equal(4, audits.Count(audit => audit.Action.StartsWith("CardStateChangedTo")));
        Assert.All(
            audits,
            audit =>
            {
                Assert.Equal("anna.nowak", audit.Actor);
                Assert.Equal("FleetCard", audit.EntityType);
                Assert.True(audit.Succeeded);
                Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
            }
        );
    }

    [Theory]
    [InlineData("{\"state\":\"active\"}")]
    [InlineData("{\"state\":\"Retired\"}")]
    [InlineData("{\"state\":\"1\"}")]
    [InlineData("{\"state\":1}")]
    public async Task Undefined_or_incorrectly_cased_states_are_bad_requests(string body)
    {
        using var host = CreateCardHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        using var request = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PutAsync($"/api/cards/{SeededCardId}/state", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Card_routes_map_invalid_missing_and_lifecycle_conflicts()
    {
        using var host = CreateCardHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");

        using var malformedIssue = new StringContent(
            "{\"vehicleId\":\"not-a-guid\"}",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        using var invalidIssue = await client.PostAsync("/api/cards", malformedIssue);
        using var missingVehicle = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(Guid.NewGuid())
        );
        using var missingCardHistory = await client.GetAsync(
            $"/api/cards/{Guid.NewGuid()}/state-history"
        );
        using var missingCardState = await client.PutAsJsonAsync(
            $"/api/cards/{Guid.NewGuid()}/state",
            new ChangeFleetCardStateRequest("Active")
        );
        using var removedAssignmentRoute = await client.PutAsync(
            $"/api/cards/{SeededCardId}/assignment",
            null
        );

        Assert.Equal(HttpStatusCode.BadRequest, invalidIssue.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingVehicle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingCardHistory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingCardState.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedAssignmentRoute.StatusCode);

        using var archiveVehicle = await client.PostAsync(
            $"/api/cars/{SecondVehicleId}/archive",
            null
        );
        Assert.Equal(HttpStatusCode.OK, archiveVehicle.StatusCode);
        using var issueArchivedVehicle = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        Assert.Equal(HttpStatusCode.Conflict, issueArchivedVehicle.StatusCode);

        using var issued = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(FirstVehicleId)
        );
        var issuedCard = (await issued.Content.ReadFromJsonAsync<FleetCardApiResponse>())!;
        using var archiveCard = await client.PutAsJsonAsync(
            $"/api/cards/{issuedCard.Id}/state",
            new ChangeFleetCardStateRequest("Archived")
        );
        Assert.Equal(HttpStatusCode.OK, archiveCard.StatusCode);

        using var restoreVehicle = await client.PostAsync(
            $"/api/cars/{SecondVehicleId}/restore",
            null
        );
        Assert.Equal(HttpStatusCode.OK, restoreVehicle.StatusCode);

        using var reactivateCard = await client.PutAsJsonAsync(
            $"/api/cards/{issuedCard.Id}/state",
            new ChangeFleetCardStateRequest("Active")
        );
        Assert.Equal(HttpStatusCode.OK, reactivateCard.StatusCode);
    }

    [Fact]
    public async Task Card_mutations_reject_missing_and_invalid_csrf_tokens()
    {
        using var host = CreateCardHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");
        var csrfToken = client.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();

        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var missingIssueCsrf = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        using var missingStateCsrf = await client.PutAsJsonAsync(
            $"/api/cards/{SeededCardId}/state",
            new ChangeFleetCardStateRequest("Blocked")
        );
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "not-the-cookie-token");
        using var invalidIssueCsrf = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        using var invalidStateCsrf = await client.PutAsJsonAsync(
            $"/api/cards/{SeededCardId}/state",
            new ChangeFleetCardStateRequest("Blocked")
        );
        Assert.All(
            new[] { missingIssueCsrf, missingStateCsrf },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode)
        );
        Assert.All(
            new[] { invalidIssueCsrf, invalidStateCsrf },
            response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode)
        );
        Assert.NotEqual("not-the-cookie-token", csrfToken);
    }

    [Fact]
    public async Task Card_responses_persisted_entities_and_logs_omit_payment_credentials()
    {
        const string fullPan = "4999888877776666";
        const string cvv = "CVV_SENTINEL_918";
        const string pin = "PIN_SENTINEL_5731";
        var loggerProvider = new CapturingLoggerProvider();
        using var host = CreateCardHost(loggerProvider);
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");

        using var issue = await client.PostAsJsonAsync(
            "/api/cards",
            new IssueFleetCardRequest(SecondVehicleId)
        );
        var body = await issue.Content.ReadAsStringAsync();
        using var cards = await client.GetAsync("/api/cards");
        body += await cards.Content.ReadAsStringAsync();
        var issued = (await issue.Content.ReadFromJsonAsync<FleetCardApiResponse>())!;
        using var history = await client.GetAsync($"/api/cards/{issued.Id}/state-history");
        body += await history.Content.ReadAsStringAsync();

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var persistedEntities = JsonSerializer.Serialize(
            new
            {
                Cards = await dbContext.Cards.AsNoTracking().ToArrayAsync(),
                Vehicles = await dbContext.Cars.AsNoTracking().ToArrayAsync(),
                Transactions = await dbContext.Transactions.AsNoTracking().ToArrayAsync(),
                Assignments = await dbContext.CardAssignmentEvents.AsNoTracking().ToArrayAsync(),
                Audits = await dbContext.AuditEvents.AsNoTracking().ToArrayAsync(),
            }
        );
        var logs = string.Join("\n", loggerProvider.Messages);

        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        Assert.StartsWith("**** **** **** ", issued.MaskedPan);
        Assert.DoesNotContain(fullPan, body, StringComparison.Ordinal);
        Assert.DoesNotContain(cvv, body, StringComparison.Ordinal);
        Assert.DoesNotContain(pin, body, StringComparison.Ordinal);
        Assert.DoesNotContain(fullPan, persistedEntities, StringComparison.Ordinal);
        Assert.DoesNotContain(cvv, persistedEntities, StringComparison.Ordinal);
        Assert.DoesNotContain(pin, persistedEntities, StringComparison.Ordinal);
        Assert.DoesNotContain(fullPan, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(cvv, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(pin, logs, StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> CreateCardHost(ILoggerProvider? loggerProvider = null)
    {
        var databaseName = $"fleet-cards-{Guid.NewGuid()}";
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
            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
            }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFleetCardService>();
                services.RemoveAll<IFleetCardsReadService>();
                services.RemoveAll<IFleetVehicleService>();
                services.AddDbContext<FleetCardsDbContext>(options =>
                    options
                        .UseInMemoryDatabase(databaseName)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
                        )
                );
                services.AddScoped<IFleetCardService, EfFleetCardService>();
                services.AddScoped<IFleetCardsReadService, EfFleetCardsReadService>();
                services.AddScoped<IFleetVehicleService, EfFleetVehicleService>();
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

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal ConcurrentBag<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => messages.Add(formatter(state, exception));
    }
}
