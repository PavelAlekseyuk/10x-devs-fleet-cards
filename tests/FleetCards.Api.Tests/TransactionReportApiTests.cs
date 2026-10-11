using System.Net;
using System.Net.Http.Json;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetCards.Api.Tests;

public sealed class TransactionReportApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid CardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VehicleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly WebApplicationFactory<Program> factory;

    public TransactionReportApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Seeded_reports_are_authenticated_readable_and_reconcile_without_card_data()
    {
        using var anonymousClient = factory.CreateClient();
        using var anonymousReport = await anonymousClient.GetAsync(ReportUri(2026, 9));
        using var anonymousSummary = await anonymousClient.GetAsync(SummaryUri(2026, 9));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReport.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSummary.StatusCode);

        foreach (
            var (userName, password) in new[]
            {
                ("piotr.wisniewski", "Piotr123!"),
                ("anna.nowak", "Anna123!"),
            }
        )
        {
            using var client = await SignInAsync(factory, userName, password);
            using var reportResponse = await client.GetAsync(ReportUri(2026, 9));
            using var summaryResponse = await client.GetAsync(SummaryUri(2026, 9));

            Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
            var report = (
                await reportResponse.Content.ReadFromJsonAsync<TransactionReportResponse>()
            )!;
            var summary = (
                await summaryResponse.Content.ReadFromJsonAsync<DashboardSummaryResponse>()
            )!;
            var row = Assert.Single(report.Transactions);

            Assert.Equal(335.93m, report.Totals.GrossTotal);
            Assert.Equal(273.11m, report.Totals.NetTotal);
            Assert.Equal(62.82m, report.Totals.VatTotal);
            Assert.Equal(0m, report.Totals.GrossSavingsTotal);
            Assert.Equal(report.Totals, summary.PeriodTotals);
            Assert.Equal(
                report.Totals.GrossTotal,
                report.Transactions.Sum(item => item.GrossTotal)
            );
            Assert.Equal(2, summary.ActiveVehicleCount);
            Assert.Equal(1, summary.ActiveCardCount);
            Assert.Equal(
                new DateTimeOffset(2026, 9, 9, 14, 0, 0, TimeSpan.FromHours(2)),
                row.UserOccurredAt
            );
            Assert.Equal(
                new DateTimeOffset(2026, 9, 9, 14, 0, 0, TimeSpan.FromHours(2)),
                row.PosOccurredAtLocal
            );

            var responseBody =
                await reportResponse.Content.ReadAsStringAsync()
                + await summaryResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("maskedPan", responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "providerToken",
                responseBody,
                StringComparison.OrdinalIgnoreCase
            );
            Assert.DoesNotContain("mock-card-4242", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain("CVV", responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PIN", responseBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("year=2026&month=9", "TimeZoneId")]
    [InlineData("month=9&timeZone=Europe%2FWarsaw", "Year")]
    [InlineData("year=2026&timeZone=Europe%2FWarsaw", "Month")]
    [InlineData("year=nope&month=9&timeZone=Europe%2FWarsaw", "Year")]
    [InlineData("year=9999&month=9&timeZone=Europe%2FWarsaw", "Year")]
    [InlineData("year=2026&month=13&timeZone=Europe%2FWarsaw", "Month")]
    [InlineData("year=2026&month=9&timeZone=Not%2FA-Timezone", "TimeZoneId")]
    public async Task Both_report_endpoints_return_structured_bad_requests_for_invalid_periods(
        string query,
        string errorKey
    )
    {
        using var client = await SignInAsync(factory, "piotr.wisniewski", "Piotr123!");

        foreach (var endpoint in new[] { "/api/transactions/report", "/api/dashboard/summary" })
        {
            using var response = await client.GetAsync($"{endpoint}?{query}");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("\"errors\"", body, StringComparison.Ordinal);
            Assert.Contains($"\"{errorKey}\"", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Empty_seeded_period_returns_zero_rows_and_matching_zero_summary()
    {
        using var client = await SignInAsync(factory, "piotr.wisniewski", "Piotr123!");
        using var reportResponse = await client.GetAsync(ReportUri(2027, 1));
        using var summaryResponse = await client.GetAsync(SummaryUri(2027, 1));

        var report = (await reportResponse.Content.ReadFromJsonAsync<TransactionReportResponse>())!;
        var summary = (
            await summaryResponse.Content.ReadFromJsonAsync<DashboardSummaryResponse>()
        )!;

        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.Empty(report.Transactions);
        Assert.Equal(new TransactionReportTotals(0m, 0m, 0m, 0m, 0), report.Totals);
        Assert.Equal(report.Totals, summary.PeriodTotals);
    }

    [Fact]
    public async Task Ef_reports_resolve_purchase_versions_and_cross_month_signed_corrections()
    {
        using var host = CreateEfHost();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetCardsDbContext>();
            dbContext.Transactions.AddRange(
                CreatePurchase(
                    Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero),
                    180m,
                    41.40m,
                    221.40m,
                    TransactionStatus.Completed,
                    version: 1,
                    quantity: 10m,
                    unitPrice: 20m,
                    discount: 2m
                ),
                CreatePurchase(
                    Guid.Parse("70000000-0000-0000-0000-000000000002"),
                    new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero),
                    190m,
                    43.70m,
                    233.70m,
                    TransactionStatus.Recalculated,
                    version: 2,
                    correctsTransactionId: Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    quantity: 10m,
                    unitPrice: 20m,
                    discount: 1m
                ),
                CreateCorrection(
                    Guid.Parse("70000000-0000-0000-0000-000000000003"),
                    Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    TransactionStatus.Refunded,
                    -9m,
                    -2.07m,
                    -11.07m,
                    quantity: -0.5m,
                    unitPrice: 20m,
                    discount: 2m
                ),
                CreateCorrection(
                    Guid.Parse("70000000-0000-0000-0000-000000000004"),
                    Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    TransactionStatus.Refunded,
                    -13.5m,
                    -3.11m,
                    -16.61m,
                    quantity: -0.75m,
                    unitPrice: 20m,
                    discount: 2m
                ),
                CreateCorrection(
                    Guid.Parse("70000000-0000-0000-0000-000000000005"),
                    Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    TransactionStatus.Cancelled,
                    -4.5m,
                    -1.04m,
                    -5.54m,
                    quantity: -0.25m,
                    unitPrice: 20m,
                    discount: 2m
                )
            );
            await dbContext.SaveChangesAsync();
        }

        using var client = await SignInAsync(host, "piotr.wisniewski", "Piotr123!");
        using var januaryResponse = await client.GetAsync(ReportUri(2027, 1, "UTC"));
        using var februaryResponse = await client.GetAsync(ReportUri(2027, 2, "UTC"));
        using var summaryResponse = await client.GetAsync(SummaryUri(2027, 2, "UTC"));

        Assert.Equal(HttpStatusCode.OK, januaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, februaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var january = (
            await januaryResponse.Content.ReadFromJsonAsync<TransactionReportResponse>()
        )!;
        var february = (
            await februaryResponse.Content.ReadFromJsonAsync<TransactionReportResponse>()
        )!;
        var summary = (
            await summaryResponse.Content.ReadFromJsonAsync<DashboardSummaryResponse>()
        )!;

        var effectivePurchase = Assert.Single(january.Transactions);
        Assert.Equal(Guid.Parse("70000000-0000-0000-0000-000000000002"), effectivePurchase.Id);
        Assert.Equal(233.70m, january.Totals.GrossTotal);
        Assert.Equal(12.30m, january.Totals.GrossSavingsTotal);
        Assert.Equal(
            new Guid[]
            {
                Guid.Parse("70000000-0000-0000-0000-000000000003"),
                Guid.Parse("70000000-0000-0000-0000-000000000004"),
                Guid.Parse("70000000-0000-0000-0000-000000000005"),
            },
            february.Transactions.Select(row => row.Id)
        );
        Assert.Equal(-33.22m, february.Totals.GrossTotal);
        Assert.Equal(-3.68m, february.Totals.GrossSavingsTotal);
        Assert.Equal(
            february.Transactions.Sum(row => row.GrossTotal),
            summary.PeriodTotals.GrossTotal
        );
        Assert.Equal(february.Totals, summary.PeriodTotals);
    }

    private WebApplicationFactory<Program> CreateEfHost()
    {
        var databaseName = $"fleet-report-{Guid.NewGuid()}";
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
                services.RemoveAll<IFleetCardService>();
                services.RemoveAll<IFleetCardsReadService>();
                services.RemoveAll<IFleetVehicleService>();
                services.AddDbContext<FleetCardsDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName)
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

    private static string ReportUri(int year, int month, string timeZone = "Europe/Warsaw") =>
        $"/api/transactions/report?year={year}&month={month}&timeZone={Uri.EscapeDataString(timeZone)}";

    private static string SummaryUri(int year, int month, string timeZone = "Europe/Warsaw") =>
        $"/api/dashboard/summary?year={year}&month={month}&timeZone={Uri.EscapeDataString(timeZone)}";

    private static FuelTransaction CreatePurchase(
        Guid id,
        DateTimeOffset occurredAt,
        decimal net,
        decimal vat,
        decimal gross,
        TransactionStatus status,
        int version,
        Guid? correctsTransactionId = null,
        decimal quantity = 1m,
        decimal? unitPrice = null,
        decimal discount = 0m
    ) =>
        new()
        {
            Id = id,
            LineageId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            Version = version,
            CorrectsTransactionId = correctsTransactionId,
            CardId = CardId,
            VehicleId = VehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = occurredAt,
            PosOccurredAtLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                occurredAt,
                "Europe/Warsaw"
            ),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Report Test Station",
            FuelType = "Diesel",
            Quantity = quantity,
            UnitPrice = unitPrice ?? net,
            Discount = discount,
            NetTotal = net,
            VatRate = 0.23m,
            VatAmount = vat,
            GrossTotal = gross,
            Status = status,
        };

    private static FuelTransaction CreateCorrection(
        Guid id,
        Guid lineageId,
        TransactionStatus status,
        decimal net,
        decimal vat,
        decimal gross,
        decimal quantity,
        decimal unitPrice,
        decimal discount = 0m
    )
    {
        var occurredAt = new DateTimeOffset(2027, 2, 2, 9, 30, 0, TimeSpan.Zero);
        return new FuelTransaction
        {
            Id = id,
            LineageId = lineageId,
            CorrectsTransactionId = lineageId,
            CardId = CardId,
            VehicleId = VehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = occurredAt,
            PosOccurredAtLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                occurredAt,
                "Europe/Warsaw"
            ),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Report Test Station",
            FuelType = "Diesel",
            Quantity = quantity,
            UnitPrice = unitPrice,
            Discount = discount,
            NetTotal = net,
            VatRate = 0.23m,
            VatAmount = vat,
            GrossTotal = gross,
            Status = status,
        };
    }
}
