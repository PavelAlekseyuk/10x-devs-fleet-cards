using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FleetCards.Application.Tests;

public sealed class TransactionReportingTests
{
    private static readonly Guid CardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VehicleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PurchaseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly TransactionReportingService reportingService = new();

    [Fact]
    public void Generated_transactions_are_completed_single_versions_with_deterministic_pos_facts()
    {
        var generator = new MockFuelTransactionGenerator();
        var generated = generator.Generate(
            CardId,
            new FleetCar
            {
                Id = VehicleId,
                RegistrationPlate = "WA 12345",
                FuelType = "Diesel",
            },
            new DateTimeOffset(2026, 9, 26, 10, 30, 0, TimeSpan.Zero)
        );

        Assert.NotEmpty(generated);
        Assert.All(
            generated,
            transaction =>
            {
                Assert.Equal(TransactionStatus.Completed, transaction.Status);
                Assert.Equal(transaction.Id, transaction.LineageId);
                Assert.Equal(1, transaction.Version);
                Assert.Null(transaction.CorrectsTransactionId);
                Assert.Equal(TimeSpan.Zero, transaction.OccurredAt.Offset);
                Assert.Equal("Europe/Warsaw", transaction.PosTimeZoneId);
                Assert.Equal(
                    TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                        transaction.OccurredAt,
                        "Europe/Warsaw"
                    ),
                    transaction.PosOccurredAtLocal
                );
            }
        );
    }

    [Fact]
    public void Report_uses_latest_purchase_version_and_adds_each_signed_correction_once()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var purchase = CreateTransaction(PurchaseId, occurredAt, 100m, 23m, 123m);
        var recalculation = CreateRecalculation(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            PurchaseId,
            occurredAt,
            200m,
            46m,
            246m,
            version: 2
        );
        var laterRecalculation = CreateRecalculation(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            PurchaseId,
            occurredAt,
            300m,
            69m,
            369m,
            version: 3
        );
        var firstRefund = CreateCorrection(
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            PurchaseId,
            new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero),
            TransactionStatus.Refunded,
            -25m,
            -5.75m,
            -30.75m
        );
        var secondRefund = CreateCorrection(
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            PurchaseId,
            new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero),
            TransactionStatus.Refunded,
            -15m,
            -3.45m,
            -18.45m
        );
        var cancellation = CreateCorrection(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            PurchaseId,
            new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero),
            TransactionStatus.Cancelled,
            -10m,
            -2.30m,
            -12.30m
        );
        var allFacts = new[]
        {
            purchase,
            recalculation,
            laterRecalculation,
            firstRefund,
            secondRefund,
            cancellation,
        };

        var september = reportingService.CreateReport(
            reportingService.ResolvePeriod(new TransactionReportRequest(2026, 9)),
            allFacts
        );
        var october = reportingService.CreateSummary(
            reportingService.ResolvePeriod(new TransactionReportRequest(2026, 10)),
            allFacts,
            activeVehicleCount: 2,
            activeCardCount: 3
        );

        var septemberRow = Assert.Single(september.Transactions);
        Assert.Equal(laterRecalculation.Id, septemberRow.Id);
        Assert.Equal(3, septemberRow.Version);
        Assert.Equal(new TransactionReportTotals(300m, 69m, 369m, 1), september.Totals);
        Assert.Equal(2, october.ActiveVehicleCount);
        Assert.Equal(3, october.ActiveCardCount);
        Assert.Equal(new TransactionReportTotals(-50m, -11.50m, -61.50m, 3), october.PeriodTotals);
    }

    [Fact]
    public void Duplicate_purchase_versions_are_rejected()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var purchase = CreateTransaction(PurchaseId, occurredAt, 100m, 23m, 123m);
        var first = CreateRecalculation(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            PurchaseId,
            occurredAt,
            200m,
            46m,
            246m,
            version: 2
        );
        var second = CreateRecalculation(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            PurchaseId,
            occurredAt,
            300m,
            69m,
            369m,
            version: 2
        );

        Assert.Throws<ArgumentException>(() =>
            reportingService.CreateReport(
                reportingService.ResolvePeriod(new TransactionReportRequest(2026, 9)),
                [second, purchase, first]
            )
        );
    }

    [Fact]
    public void Period_uses_local_midnight_boundaries_and_accounts_for_daylight_saving()
    {
        var period = reportingService.ResolvePeriod(new TransactionReportRequest(2026, 3));

        Assert.Equal(new DateTimeOffset(2026, 2, 28, 23, 0, 0, TimeSpan.Zero), period.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 22, 0, 0, TimeSpan.Zero), period.EndUtc);
        Assert.Equal(TimeSpan.FromHours(743), period.EndUtc - period.StartUtc);
    }

    [Fact]
    public void Default_timezone_is_cet_compatible_and_can_be_configured()
    {
        var defaultPeriod = reportingService.ResolvePeriod(new TransactionReportRequest(2026, 3));
        var configuredPeriod = new TransactionReportingService("America/New_York").ResolvePeriod(
            new TransactionReportRequest(2026, 3)
        );

        Assert.Equal("Europe/Warsaw", defaultPeriod.TimeZoneId);
        Assert.Equal("America/New_York", configuredPeriod.TimeZoneId);
        Assert.NotEqual(defaultPeriod.StartUtc, configuredPeriod.StartUtc);
    }

    [Fact]
    public void Report_includes_half_open_period_edges_and_keeps_pos_time_separate_from_user_time()
    {
        var period = reportingService.ResolvePeriod(new TransactionReportRequest(2026, 3));
        var atStart = CreateTransaction(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            period.StartUtc,
            10m,
            2.30m,
            12.30m
        );
        var atEnd = CreateTransaction(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            period.EndUtc,
            20m,
            4.60m,
            24.60m
        );
        var boundaryTransaction = CreateTransaction(
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero),
            30m,
            6.90m,
            36.90m
        );
        boundaryTransaction.PosOccurredAtLocal = new DateTimeOffset(
            2026,
            3,
            29,
            2,
            30,
            0,
            TimeSpan.FromHours(2)
        );

        var report = reportingService.CreateReport(period, [atEnd, boundaryTransaction, atStart]);

        Assert.Equal(2, report.Totals.RowCount);
        Assert.Equal(
            new[] { atStart.Id, boundaryTransaction.Id },
            report.Transactions.Select(row => row.Id)
        );
        var boundaryRow = report.Transactions.Single(row => row.Id == boundaryTransaction.Id);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(1)),
            boundaryRow.UserOccurredAt
        );
        Assert.Equal(boundaryTransaction.PosOccurredAtLocal, boundaryRow.PosOccurredAtLocal);
        Assert.Equal("Europe/Warsaw", boundaryRow.PosTimeZoneId);
    }

    [Fact]
    public void Derived_money_uses_away_from_zero_rounding_and_totals_reconcile_visible_rows()
    {
        Assert.Equal(1.01m, TransactionReportingService.CalculateNetTotal(1m, 1.005m, 0m));
        Assert.Equal(0.01m, TransactionReportingService.CalculateVatAmount(0.05m, 0.1m));

        var first = CreateTransaction(
            PurchaseId,
            new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
            10.01m,
            2.30m,
            12.31m
        );
        var second = CreateTransaction(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero),
            20.02m,
            4.60m,
            24.62m
        );

        var report = reportingService.CreateReport(
            reportingService.ResolvePeriod(new TransactionReportRequest(2026, 9)),
            [first, second]
        );

        Assert.Equal(report.Transactions.Sum(row => row.NetTotal), report.Totals.NetTotal);
        Assert.Equal(report.Transactions.Sum(row => row.VatAmount), report.Totals.VatTotal);
        Assert.Equal(report.Transactions.Sum(row => row.GrossTotal), report.Totals.GrossTotal);
        Assert.Equal(report.Totals.NetTotal + report.Totals.VatTotal, report.Totals.GrossTotal);
    }

    [Theory]
    [InlineData(2026, 0, "Europe/Warsaw")]
    [InlineData(2026, 13, "Europe/Warsaw")]
    [InlineData(0, 1, "Europe/Warsaw")]
    [InlineData(2026, 1, "Not/A-Timezone")]
    [InlineData(2026, 1, "Central European Standard Time")]
    public void Invalid_periods_and_non_Iana_timezone_ids_are_rejected(
        int year,
        int month,
        string timeZoneId
    )
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            reportingService.ResolvePeriod(new TransactionReportRequest(year, month, timeZoneId))
        );
    }

    [Fact]
    public async Task Ef_model_and_seed_preserve_transaction_facts_and_lineage_constraints()
    {
        var options = new DbContextOptionsBuilder<FleetCardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new FleetCardsDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var entity = context
            .GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(FuelTransaction))!;
        var lineageIndex = Assert.Single(entity.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            new[] { nameof(FuelTransaction.LineageId), nameof(FuelTransaction.Version) },
            lineageIndex.Properties.Select(property => property.Name)
        );
        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Transactions_LineageVersion"
        );
        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Transactions_OccurredAtUtc"
        );

        var seeded = await context.Transactions.SingleAsync(transaction =>
            transaction.Id == PurchaseId
        );
        Assert.Equal(PurchaseId, seeded.LineageId);
        Assert.Equal(1, seeded.Version);
        Assert.Null(seeded.CorrectsTransactionId);
        Assert.Equal(42.081m, seeded.Quantity);
        Assert.Equal(265.53m, seeded.NetTotal);
        Assert.Equal(61.07m, seeded.VatAmount);
        Assert.Equal(326.60m, seeded.GrossTotal);
        Assert.Equal(
            seeded.NetTotal,
            TransactionReportingService.CalculateNetTotal(
                seeded.Quantity,
                seeded.UnitPrice,
                seeded.Discount
            )
        );
        Assert.Equal(
            seeded.VatAmount,
            TransactionReportingService.CalculateVatAmount(seeded.NetTotal, seeded.VatRate)
        );
        Assert.Equal(seeded.NetTotal + seeded.VatAmount, seeded.GrossTotal);
        Assert.Equal(VehicleId, seeded.VehicleId);
        Assert.Equal("WA 12345", seeded.VehiclePlate);
        Assert.Equal("Europe/Warsaw", seeded.PosTimeZoneId);
        Assert.Equal(TimeSpan.FromHours(2), seeded.PosOccurredAtLocal.Offset);
    }

    private static FuelTransaction CreateTransaction(
        Guid id,
        DateTimeOffset occurredAt,
        decimal net,
        decimal vat,
        decimal gross
    ) =>
        new()
        {
            Id = id,
            LineageId = id,
            Version = 1,
            CardId = CardId,
            VehicleId = VehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = occurredAt.ToUniversalTime(),
            PosOccurredAtLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                occurredAt,
                "Europe/Warsaw"
            ),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Test Station",
            FuelType = "Diesel",
            Quantity = 1m,
            UnitPrice = net,
            Discount = 0m,
            NetTotal = net,
            VatRate = 0.23m,
            VatAmount = vat,
            GrossTotal = gross,
            Status = TransactionStatus.Completed,
        };

    private static FuelTransaction CreateRecalculation(
        Guid id,
        Guid lineageId,
        DateTimeOffset occurredAt,
        decimal net,
        decimal vat,
        decimal gross,
        int version
    ) =>
        new()
        {
            Id = id,
            LineageId = lineageId,
            Version = version,
            CorrectsTransactionId = lineageId,
            CardId = CardId,
            VehicleId = VehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = occurredAt.ToUniversalTime(),
            PosOccurredAtLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                occurredAt,
                "Europe/Warsaw"
            ),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Test Station",
            FuelType = "Diesel",
            Quantity = 1m,
            UnitPrice = net,
            NetTotal = net,
            VatRate = 0.23m,
            VatAmount = vat,
            GrossTotal = gross,
            Status = TransactionStatus.Recalculated,
        };

    private static FuelTransaction CreateCorrection(
        Guid id,
        Guid lineageId,
        DateTimeOffset occurredAt,
        TransactionStatus status,
        decimal net,
        decimal vat,
        decimal gross
    ) =>
        new()
        {
            Id = id,
            LineageId = lineageId,
            CorrectsTransactionId = lineageId,
            CardId = CardId,
            VehicleId = VehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = occurredAt.ToUniversalTime(),
            PosOccurredAtLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                occurredAt,
                "Europe/Warsaw"
            ),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Test Station",
            FuelType = "Diesel",
            Quantity = 0m,
            UnitPrice = 0m,
            NetTotal = net,
            VatRate = 0.23m,
            VatAmount = vat,
            GrossTotal = gross,
            Status = status,
        };
}
