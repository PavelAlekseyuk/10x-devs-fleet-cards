using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Application.Tests;

public sealed class ProgramComparisonTests
{
    private static readonly Guid AlphaProgramId = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );
    private static readonly Guid BetaProgramId = Guid.Parse(
        "55555555-5555-5555-5555-555555555556"
    );

    [Fact]
    public async Task Comparison_uses_actual_baseline_and_calculates_hypothetical_totals_and_savings()
    {
        var transaction = CreateRow(
            quantity: 2m,
            unitPrice: 10m,
            netTotal: 19m,
            vatAmount: 4.37m,
            grossTotal: 23.37m
        );
        var service = CreateService(
            [transaction],
            [
                CreateProgram(AlphaProgramId, "10% Program"),
                CreateProgram(BetaProgramId, "Negative Program"),
            ],
            [
                new FleetCardProgramRule
                {
                    ProgramId = AlphaProgramId,
                    FuelType = "Diesel",
                    Percent = 0.10m,
                },
                new FleetCardProgramRule
                {
                    ProgramId = BetaProgramId,
                    FuelType = "Diesel",
                    Percent = -0.10m,
                },
            ]
        );

        var result = await service.GetComparisonAsync(
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero)
        );

        Assert.Equal(new ProgramComparisonBaseline(19m, 4.37m, 23.37m), result.Baseline);
        var alpha = Assert.Single(
            result.Programs,
            program => program.ProgramId == AlphaProgramId
        );
        Assert.Equal(18m, alpha.HypotheticalNetTotal);
        Assert.Equal(22.14m, alpha.HypotheticalGrossTotal);
        Assert.Equal(1m, alpha.NetSavings);
        Assert.Equal(1.23m, alpha.GrossSavings);

        var beta = Assert.Single(
            result.Programs,
            program => program.ProgramId == BetaProgramId
        );
        Assert.Equal(22m, beta.HypotheticalNetTotal);
        Assert.Equal(27.06m, beta.HypotheticalGrossTotal);
        Assert.Equal(-3m, beta.NetSavings);
        Assert.Equal(-3.69m, beta.GrossSavings);
    }

    [Fact]
    public async Task Empty_period_returns_zero_totals_and_breaks_gross_ties_by_program_name()
    {
        var service = CreateService(
            [],
            [
                CreateProgram(BetaProgramId, "Voyager"),
                CreateProgram(AlphaProgramId, "Alpha"),
            ],
            []
        );

        var result = await service.GetComparisonAsync(
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero)
        );

        Assert.Equal(new ProgramComparisonBaseline(0m, 0m, 0m), result.Baseline);
        Assert.Equal(
            new[] { "Alpha", "Voyager" },
            result.Programs.Select(program => program.ProgramName)
        );
        Assert.All(
            result.Programs,
            program =>
            {
                Assert.Equal(0m, program.HypotheticalNetTotal);
                Assert.Equal(0m, program.HypotheticalGrossTotal);
                Assert.Equal(0m, program.NetSavings);
                Assert.Equal(0m, program.GrossSavings);
            }
        );
    }

    [Fact]
    public async Task Comparison_rejects_an_empty_or_reversed_range()
    {
        var service = CreateService([], [], []);
        var from = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetComparisonAsync(from, from));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetComparisonAsync(from.AddDays(1), from)
        );
    }

    private static ProgramComparisonService CreateService(
        IReadOnlyList<TransactionReportRowResponse> rows,
        IReadOnlyList<FleetCardProgramResponse> programs,
        IReadOnlyList<FleetCardProgramRule> rules
    ) =>
        new(
            new TestReadService(rows, programs),
            new TransactionReportingService(),
            new TestProgramResolutionDataSource(rules)
        );

    private static FleetCardProgramResponse CreateProgram(Guid id, string name) =>
        new(id, name, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null);

    private static TransactionReportRowResponse CreateRow(
        decimal quantity,
        decimal unitPrice,
        decimal netTotal,
        decimal vatAmount,
        decimal grossTotal
    )
    {
        var occurredAt = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
        return new TransactionReportRowResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            null,
            Guid.NewGuid(),
            null,
            "WA 12345",
            occurredAt,
            occurredAt,
            occurredAt,
            "UTC",
            "Comparison Test Station",
            "Diesel",
            quantity,
            unitPrice,
            0.5m,
            netTotal,
            0.23m,
            vatAmount,
            grossTotal,
            TransactionStatus.Completed.ToString()
        );
    }

    private sealed class TestProgramResolutionDataSource(
        IReadOnlyList<FleetCardProgramRule> rules
    ) : IProgramResolutionDataSource
    {
        public IReadOnlyList<FleetCardProgramRule> GetRules() => rules;

        public IReadOnlyList<ClientProgramAssignment> GetAssignments() => [];
    }

    private sealed class TestReadService(
        IReadOnlyList<TransactionReportRowResponse> rows,
        IReadOnlyList<FleetCardProgramResponse> programs
    ) : IFleetCardsReadService
    {
        public CurrentUserResponse GetCurrentUser() => throw new NotImplementedException();

        public ClientResponse GetClient() => throw new NotImplementedException();

        public IReadOnlyList<FleetCardResponse> GetCards() => throw new NotImplementedException();

        public Task<TransactionReportResponse> GetTransactionReportAsync(
            TransactionReportPeriod period,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new TransactionReportResponse(
                    period.Year,
                    period.Month,
                    period.TimeZoneId,
                    period.StartUtc,
                    period.EndUtc,
                    rows,
                    new TransactionReportTotals(0m, 0m, 0m, 0m, rows.Count)
                )
            );
        }

        public Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
            TransactionReportPeriod period,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public IReadOnlyList<FleetCardProgramResponse> GetPrograms() => programs;
    }
}
