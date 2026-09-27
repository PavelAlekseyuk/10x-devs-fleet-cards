using System.Net;
using System.Net.Http.Json;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetCards.Api.Tests;

public sealed class FleetVehicleApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SeededCarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly WebApplicationFactory<Program> factory;

    public FleetVehicleApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Car_reads_allow_both_roles_but_mutations_require_readwrite_and_valid_csrf()
    {
        using var host = CreateVehicleHost();
        using var anonymousClient = host.CreateClient();
        using var anonymousRead = await anonymousClient.GetAsync("/api/cars");
        using var anonymousWrite = await anonymousClient.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("ANON-1", "95", null, null)
        );

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRead.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrite.StatusCode);

        using var readOnlyClient = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");
        using var readOnlyRead = await readOnlyClient.GetAsync("/api/cars");
        using var readOnlyWrite = await readOnlyClient.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("READONLY-1", "95", null, null)
        );

        Assert.Equal(HttpStatusCode.OK, readOnlyRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readOnlyWrite.StatusCode);

        using var readWriteClient = await SignInAsync(host, "anna.nowak", "Anna123!");
        var csrfToken = readWriteClient.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();
        readWriteClient.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var invalidCsrfWrite = await readWriteClient.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("NO-CSRF-1", "95", null, null)
        );

        Assert.Equal(HttpStatusCode.Forbidden, invalidCsrfWrite.StatusCode);
        readWriteClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrfToken);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        Assert.Empty(
            await dbContext
                .AuditEvents.Where(auditEvent => auditEvent.EntityType == "FleetCar")
                .ToArrayAsync()
        );
    }

    [Fact]
    public async Task Vehicle_routes_validate_search_archive_restore_conflicts_and_write_one_audit_per_success()
    {
        using var host = CreateVehicleHost();
        using var client = await SignInAsync(host, "anna.nowak", "Anna123!");

        using var create = await client.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest(" ab\t- 123 ", "Diesel", " Volvo ", null)
        );
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<FleetCarResponse>())!;
        Assert.EndsWith($"/api/cars/{created.Id}", create.Headers.Location!.ToString());
        Assert.Equal("ab\t- 123", created.RegistrationPlate);
        Assert.DoesNotContain(
            "NormalizedRegistrationPlate",
            await create.Content.ReadAsStringAsync()
        );

        using var normalizedSearch = await client.GetAsync(
            "/api/cars?search=A%20B%20-%201%202%203"
        );
        var searchResults = (
            await normalizedSearch.Content.ReadFromJsonAsync<FleetCarResponse[]>()
        )!;
        Assert.Contains(searchResults, car => car.Id == created.Id);

        using var update = await client.PutAsJsonAsync(
            $"/api/cars/{created.Id}",
            new UpdateFleetCarRequest("AB - 123", "Diesel+", "Volvo", "FH")
        );
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<FleetCarResponse>())!;
        Assert.Equal("Diesel+", updated.FuelType);
        Assert.Equal("FH", updated.Model);

        using var archive = await client.PostAsync($"/api/cars/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        using var activeSearch = await client.GetAsync("/api/cars?search=AB-123");
        Assert.DoesNotContain(
            (await activeSearch.Content.ReadFromJsonAsync<FleetCarResponse[]>())!,
            car => car.Id == created.Id
        );
        using var archivedSearch = await client.GetAsync(
            "/api/cars?search=A%20B%20-%20123&includeArchived=true"
        );
        Assert.Contains(
            (await archivedSearch.Content.ReadFromJsonAsync<FleetCarResponse[]>())!,
            car => car.Id == created.Id && car.IsArchived
        );

        using var restore = await client.PostAsync($"/api/cars/{created.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        using var duplicate = await client.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("A B-123", "95", null, null)
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var invalid = await client.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest(" ", "95", null, null)
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("RegistrationPlate", await invalid.Content.ReadAsStringAsync());

        using var missing = await client.PutAsJsonAsync(
            $"/api/cars/{Guid.NewGuid()}",
            new UpdateFleetCarRequest("MISS-1", "95", null, null)
        );
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var original = await client.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("REST 1", "95", null, null)
        );
        var originalCar = (await original.Content.ReadFromJsonAsync<FleetCarResponse>())!;
        using var archiveOriginal = await client.PostAsync(
            $"/api/cars/{originalCar.Id}/archive",
            null
        );
        using var replacement = await client.PostAsJsonAsync(
            "/api/cars",
            new CreateFleetCarRequest("REST1", "98", null, null)
        );
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        using var restoreConflict = await client.PostAsync(
            $"/api/cars/{originalCar.Id}/restore",
            null
        );
        Assert.Equal(HttpStatusCode.Conflict, restoreConflict.StatusCode);

        using var archiveSeeded = await client.PostAsync($"/api/cars/{SeededCarId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveSeeded.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
        var audits = await dbContext
            .AuditEvents.AsNoTracking()
            .Where(eventRow => eventRow.EntityType == "FleetCar")
            .OrderBy(eventRow => eventRow.OccurredAt)
            .ToArrayAsync();
        Assert.Equal(8, audits.Length);
        Assert.Equal(3, audits.Count(eventRow => eventRow.Action == "VehicleCreated"));
        Assert.Equal(1, audits.Count(eventRow => eventRow.Action == "VehicleUpdated"));
        Assert.Equal(3, audits.Count(eventRow => eventRow.Action == "VehicleArchived"));
        Assert.Equal(1, audits.Count(eventRow => eventRow.Action == "VehicleRestored"));
        Assert.All(
            audits,
            audit =>
            {
                Assert.Equal("anna.nowak", audit.Actor);
                Assert.Equal("FleetCar", audit.EntityType);
                Assert.True(audit.Succeeded);
                Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
            }
        );

        var seededCar = await dbContext.Cars.SingleAsync(car => car.Id == SeededCarId);
        var assignedCard = await dbContext.Cards.SingleAsync(card =>
            card.AssignedCarId == SeededCarId
        );
        var transaction = await dbContext.Transactions.SingleAsync(row =>
            row.VehicleId == SeededCarId
        );
        var vehicleService = scope.ServiceProvider.GetRequiredService<IFleetVehicleService>();
        Assert.True(seededCar.IsArchived);
        Assert.Equal(SeededCarId, assignedCard.AssignedCarId);
        Assert.Equal("WA 12345", transaction.VehiclePlate);
        Assert.False(await vehicleService.IsEligibleForAssignmentAsync(SeededCarId));
    }

    private WebApplicationFactory<Program> CreateVehicleHost()
    {
        var databaseName = $"fleet-vehicles-{Guid.NewGuid()}";
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
                services.RemoveAll<IFleetVehicleService>();
                services.AddDbContext<FleetCardsDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName)
                );
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
}
