using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;

namespace FleetCards.Application.Tests;

public sealed class ProgramResolutionTests
{
    private static readonly Guid ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid FirstProgramId = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );
    private static readonly Guid SecondProgramId = Guid.Parse(
        "55555555-5555-5555-5555-555555555556"
    );
    private static readonly DateTimeOffset StartsAt = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SwitchAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetPercent_returns_the_rule_for_the_active_client_program()
    {
        var service = CreateService();

        var percent = service.GetPercent(
            ClientId,
            "Diesel",
            new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)
        );

        Assert.Equal(0.10m, percent);
    }

    [Fact]
    public void GetPercent_returns_zero_when_no_assignment_or_fuel_rule_covers_the_instant()
    {
        var service = CreateService();

        Assert.Equal(0m, service.GetPercent(ClientId, "Diesel", StartsAt.AddTicks(-1)));
        Assert.Equal(0m, service.GetPercent(Guid.NewGuid(), "Diesel", SwitchAt));
        Assert.Equal(0m, service.GetPercent(ClientId, "Unknown", SwitchAt));
    }

    [Fact]
    public void GetPercent_uses_start_inclusive_end_exclusive_assignment_boundaries()
    {
        var service = CreateService();

        Assert.Equal(0.10m, service.GetPercent(ClientId, "Diesel", StartsAt));
        Assert.Equal(0.20m, service.GetPercent(ClientId, "Diesel", SwitchAt));
    }

    [Fact]
    public void Seeded_default_assignment_uses_nebula_basic_with_zero_percent_rules()
    {
        var dataSource = new SeededProgramResolutionDataSource();
        var service = new ProgramResolutionService(dataSource);
        var vehicleService = new SeededFleetVehicleService();
        var programs = new SeededFleetCardsReadService(
            new SeededFleetCardService(
                vehicleService,
                new MockCardProvider(),
                new MockFuelTransactionGenerator()
            ),
            vehicleService,
            new TransactionReportingService(),
            dataSource
        ).GetPrograms();

        Assert.Equal(SecondProgramId, Assert.Single(dataSource.GetAssignments()).ProgramId);
        Assert.Equal(
            "Orion Diesel",
            Assert.Single(programs, program => program.Id == FirstProgramId).Name
        );
        var nebulaBasic = Assert.Single(programs, program => program.Id == SecondProgramId);
        Assert.Equal("Nebula Basic", nebulaBasic.Name);
        Assert.All(
            nebulaBasic.Rules!,
            rule =>
                Assert.Equal(
                    0m,
                    service.GetPercent(
                        ClientId,
                        rule.FuelType,
                        new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)
                    )
                )
        );
        Assert.Equal(
            0m,
            service.GetPercent(
                ClientId,
                "Diesel",
                new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)
            )
        );
        Assert.Equal(
            0m,
            service.GetPercent(
                ClientId,
                "95",
                new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)
            )
        );
    }

    [Fact]
    public void Generate_uses_the_optional_period_and_resolved_percent_for_each_transaction()
    {
        var generator = new MockFuelTransactionGenerator(programResolutionService: CreateService());
        var periodStart = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);
        var period = new TransactionReportPeriod(2026, 9, "UTC", periodStart, periodEnd);

        var transactions = generator.Generate(
            Guid.NewGuid(),
            new FleetCar
            {
                Id = Guid.NewGuid(),
                ClientId = ClientId,
                RegistrationPlate = "WA 12345",
                FuelType = "Diesel",
            },
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
            period
        );

        Assert.All(
            transactions,
            transaction =>
            {
                Assert.True(transaction.OccurredAt >= periodStart);
                Assert.True(transaction.OccurredAt < periodEnd);
                Assert.Equal(
                    decimal.Round(transaction.UnitPrice * 0.10m, 4, MidpointRounding.AwayFromZero),
                    transaction.Discount
                );
            }
        );
    }

    private static ProgramResolutionService CreateService() =>
        new(
            new TestProgramResolutionDataSource(
                [
                    new FleetCardProgramRule
                    {
                        ProgramId = FirstProgramId,
                        FuelType = "Diesel",
                        Percent = 0.10m,
                    },
                    new FleetCardProgramRule
                    {
                        ProgramId = SecondProgramId,
                        FuelType = "Diesel",
                        Percent = 0.20m,
                    },
                ],
                [
                    new ClientProgramAssignment
                    {
                        Id = Guid.NewGuid(),
                        ClientId = ClientId,
                        ProgramId = FirstProgramId,
                        StartsAt = StartsAt,
                        EndsAt = SwitchAt,
                    },
                    new ClientProgramAssignment
                    {
                        Id = Guid.NewGuid(),
                        ClientId = ClientId,
                        ProgramId = SecondProgramId,
                        StartsAt = SwitchAt,
                    },
                ]
            )
        );

    private sealed class TestProgramResolutionDataSource(
        IReadOnlyList<FleetCardProgramRule> rules,
        IReadOnlyList<ClientProgramAssignment> assignments
    ) : IProgramResolutionDataSource
    {
        public IReadOnlyList<FleetCardProgramRule> GetRules() => rules;

        public IReadOnlyList<ClientProgramAssignment> GetAssignments() => assignments;
    }
}
