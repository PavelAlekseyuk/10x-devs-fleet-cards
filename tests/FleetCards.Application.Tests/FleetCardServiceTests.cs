using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetCards.Application.Tests;

public sealed class FleetCardServiceTests
{
    private static readonly Guid PrimaryVehicleId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly Guid SecondVehicleId = Guid.Parse(
        "33333333-3333-3333-3333-333333333333"
    );
    private static readonly Guid SeededCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Issue_creates_safe_card_assignment_audit_and_completed_transactions()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issuedAt = DateTimeOffset.UtcNow;

        var result = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-card"
        );

        Assert.True(result.IsSuccess);
        var card = result.Card!;
        Assert.StartsWith("**** **** **** ", card.MaskedPan);
        Assert.StartsWith("test-card-", card.ProviderToken);
        Assert.DoesNotContain("4111111111111111", card.ToString());
        Assert.Equal(PrimaryVehicleId, card.AssignedCarId);

        var transactions = await context
            .Transactions.Where(transaction => transaction.CardId == card.Id)
            .ToArrayAsync();
        Assert.InRange(transactions.Length, 1, 5);
        var previousMonthStart = new DateTimeOffset(
            issuedAt.Year,
            issuedAt.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        ).AddMonths(-1);
        var currentMonthStart = previousMonthStart.AddMonths(1);
        Assert.All(
            transactions,
            transaction =>
            {
                Assert.Equal(PrimaryVehicleId, transaction.VehicleId);
                Assert.Equal("WA 12345", transaction.VehiclePlate);
                Assert.Equal("Diesel", transaction.FuelType);
                Assert.Equal(TransactionStatus.Completed, transaction.Status);
                Assert.InRange(
                    transaction.OccurredAt,
                    previousMonthStart,
                    currentMonthStart.AddTicks(-1)
                );
                Assert.Equal(
                    decimal.Round(
                        transaction.Quantity * (transaction.UnitPrice - transaction.Discount),
                        2,
                        MidpointRounding.AwayFromZero
                    ),
                    transaction.NetTotal
                );
                Assert.Equal(
                    decimal.Round(
                        transaction.NetTotal * transaction.VatRate,
                        2,
                        MidpointRounding.AwayFromZero
                    ),
                    transaction.VatAmount
                );
                Assert.Equal(transaction.NetTotal + transaction.VatAmount, transaction.GrossTotal);
            }
        );

        var assignments = await context
            .CardAssignmentEvents.Where(assignment => assignment.CardId == card.Id)
            .ToArrayAsync();
        Assert.Single(assignments);
        Assert.Equal(1, assignments[0].OrderingKey);
        Assert.Equal(TimeSpan.Zero, assignments[0].EffectiveAt.Offset);
        Assert.Contains(
            await context.AuditEvents.ToArrayAsync(),
            auditEvent => auditEvent.EntityId == card.Id && auditEvent.Action == "CardIssued"
        );
    }

    [Fact]
    public async Task Issue_rejects_missing_or_archived_vehicles_and_allows_multiple_cards_per_vehicle()
    {
        await using var context = await CreateContextAsync();
        context.Cars.Add(
            new FleetCar
            {
                Id = Guid.NewGuid(),
                ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                RegistrationPlate = "ARCHIVED 1",
                NormalizedRegistrationPlate = "ARCHIVED1",
                FuelType = "95",
                IsArchived = true,
            }
        );
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var archivedVehicleId = await context
            .Cars.Where(vehicle => vehicle.IsArchived)
            .Select(vehicle => vehicle.Id)
            .SingleAsync();

        var first = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-first"
        );
        var second = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-second"
        );
        var missing = await service.IssueAsync(
            new IssueFleetCardRequest(Guid.NewGuid()),
            "anna.nowak",
            "issue-missing"
        );
        var archived = await service.IssueAsync(
            new IssueFleetCardRequest(archivedVehicleId),
            "anna.nowak",
            "issue-archived"
        );

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Card!.Id, second.Card!.Id);
        Assert.Equal(CardCommandError.NotFound, missing.Error);
        Assert.Equal(CardCommandError.Conflict, archived.Error);
    }

    [Fact]
    public async Task Issue_rechecks_vehicle_eligibility_after_provider_issuance()
    {
        await using var context = await CreateContextAsync();
        var provider = new TestCardProvider(async () =>
        {
            var vehicle = await context.Cars.SingleAsync(car => car.Id == PrimaryVehicleId);
            vehicle.IsArchived = true;
            await context.SaveChangesAsync();
        });
        var service = CreateService(context, provider);

        var result = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-vehicle-archived-during-provider"
        );

        Assert.Equal(CardCommandError.Conflict, result.Error);
        Assert.Equal(1, await context.Cards.CountAsync());
        Assert.Equal(1, await context.CardAssignmentEvents.CountAsync());
        Assert.Equal(1, await context.Transactions.CountAsync());
    }

    [Fact]
    public async Task State_changes_accept_every_defined_state_and_archived_cards_can_be_reactivated()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-state-card"
        );
        var cardId = issue.Card!.Id;

        foreach (var state in Enum.GetNames<CardState>())
        {
            var result = await service.ChangeStateAsync(
                cardId,
                new ChangeFleetCardStateRequest(state),
                "anna.nowak",
                $"state-{state}"
            );

            Assert.True(result.IsSuccess);
            Assert.Equal(state, result.Card!.State);
            Assert.Equal(PrimaryVehicleId, result.Card.AssignedCarId);
        }

        var invalid = await service.ChangeStateAsync(
            cardId,
            new ChangeFleetCardStateRequest("active"),
            "anna.nowak",
            "state-invalid"
        );
        var audits = await context
            .AuditEvents.Where(auditEvent => auditEvent.EntityId == cardId)
            .ToArrayAsync();

        Assert.Equal(CardCommandError.Validation, invalid.Error);
        Assert.All(
            Enum.GetNames<CardState>(),
            state =>
                Assert.Contains(
                    audits,
                    auditEvent => auditEvent.Action == $"CardStateChangedTo{state}"
                )
        );
    }

    [Fact]
    public async Task Issue_time_assignment_is_immutable_and_state_changes_preserve_transaction_vehicle_snapshots()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-reassignment-card"
        );
        var cardId = issue.Card!.Id;
        var before = await context
            .Transactions.Where(transaction => transaction.CardId == cardId)
            .OrderBy(transaction => transaction.Id)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.VehicleId,
                transaction.VehiclePlate,
            })
            .ToArrayAsync();

        await service.ChangeStateAsync(
            cardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Blocked)),
            "anna.nowak",
            "block-card"
        );
        await service.ChangeStateAsync(
            cardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Stolen)),
            "anna.nowak",
            "mark-card-stolen"
        );
        var history = await service.GetStateHistoryAsync(cardId);
        var after = await context
            .Transactions.Where(transaction => transaction.CardId == cardId)
            .OrderBy(transaction => transaction.Id)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.VehicleId,
                transaction.VehiclePlate,
            })
            .ToArrayAsync();
        var card = await context.Cards.SingleAsync(candidate => candidate.Id == cardId);

        Assert.Equal(PrimaryVehicleId, card.AssignedCarId);
        Assert.Equal(before, after);
        Assert.Equal(
            new[] { "Stolen", "Blocked", "Active" },
            history.History.Select(entry => entry.State)
        );
        Assert.All(after, transaction => Assert.Equal(PrimaryVehicleId, transaction.VehicleId));
    }

    [Fact]
    public void State_history_maps_issuance_and_uses_event_id_for_equal_timestamps()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var history = CardStateHistoryProjection.Project([
            CreateAuditEvent(
                "00000000-0000-0000-0000-000000000001",
                "CardStateChangedToBlocked",
                occurredAt
            ),
            CreateAuditEvent(
                "00000000-0000-0000-0000-000000000002",
                "CardStateChangedToStolen",
                occurredAt
            ),
            CreateAuditEvent(
                "00000000-0000-0000-0000-000000000003",
                "CardIssued",
                occurredAt.AddMinutes(-1)
            ),
        ]);

        Assert.Equal(new[] { "Stolen", "Blocked", "Active" }, history.Select(entry => entry.State));
    }

    [Fact]
    public async Task Mock_provider_returns_only_masked_card_data_and_a_unique_token()
    {
        var provider = new MockCardProvider();

        var first = await provider.IssueAsync();
        var second = await provider.IssueAsync();

        Assert.Matches("^\\*{4} \\*{4} \\*{4} [0-9]{4}$", first.MaskedPan);
        Assert.NotEqual(first.ProviderToken, second.ProviderToken);
        Assert.DoesNotContain("4111111111111111", first.ToString());
        Assert.Equal("FleetCards Mock", first.Metadata.Provider);
        Assert.Equal("Virtual", first.Metadata.Product);
    }

    [Fact]
    public void Transaction_generation_is_deterministic_and_uses_the_previous_month()
    {
        var issuedAt = new DateTimeOffset(2026, 9, 26, 10, 30, 0, TimeSpan.Zero);
        var vehicle = new FleetCar
        {
            Id = SecondVehicleId,
            RegistrationPlate = "PO 98765",
            FuelType = "95+",
        };
        var generator = new MockFuelTransactionGenerator();
        var cardId = Guid.NewGuid();
        var first = generator.Generate(cardId, vehicle, issuedAt);
        var second = generator.Generate(cardId, vehicle, issuedAt);
        var previousMonthStart = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var currentMonthStart = previousMonthStart.AddMonths(1);

        Assert.InRange(first.Count, 1, 5);
        Assert.Equal(first.Select(ToStableValues), second.Select(ToStableValues));
        Assert.All(
            first,
            transaction =>
            {
                Assert.Equal("95+", transaction.FuelType);
                Assert.Equal(SecondVehicleId, transaction.VehicleId);
                Assert.Equal("PO 98765", transaction.VehiclePlate);
                Assert.Equal(TransactionStatus.Completed, transaction.Status);
                Assert.Equal(0.23m, transaction.VatRate);
                Assert.True(transaction.OccurredAt >= previousMonthStart);
                Assert.True(transaction.OccurredAt < currentMonthStart);
                Assert.Equal(
                    decimal.Round(
                        transaction.Quantity * (transaction.UnitPrice - transaction.Discount),
                        2,
                        MidpointRounding.AwayFromZero
                    ),
                    transaction.NetTotal
                );
            }
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=FleetCardsTest")]
    public void Infrastructure_registration_applies_configured_vat_rate_in_both_modes(
        string? connectionString
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:fleetcards"] = connectionString,
                    ["Transactions:VatRate"] = "0.08",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddFleetCardsInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<MockFuelTransactionGenerator>();
        var vehicle = new FleetCar
        {
            Id = SecondVehicleId,
            RegistrationPlate = "PO 98765",
            FuelType = "95+",
        };

        var transactions = generator.Generate(
            Guid.NewGuid(),
            vehicle,
            new DateTimeOffset(2026, 9, 26, 10, 30, 0, TimeSpan.Zero)
        );

        Assert.All(transactions, transaction => Assert.Equal(0.08m, transaction.VatRate));
    }

    [Fact]
    public async Task Ef_model_enforces_unique_per_card_order_and_seeds_initial_assignment()
    {
        await using var context = await CreateContextAsync();

        var entityType = context.Model.FindEntityType(typeof(FleetCardAssignmentEvent))!;
        Assert.Contains(
            entityType.GetIndexes(),
            index =>
                index.IsUnique
                && index
                    .Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(FleetCardAssignmentEvent.CardId),
                        nameof(FleetCardAssignmentEvent.OrderingKey),
                    ])
        );
        var seededCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var seededEvent = await context.CardAssignmentEvents.SingleAsync(assignment =>
            assignment.CardId == seededCardId
        );
        var earliestSeededTransaction = await context
            .Transactions.Where(transaction => transaction.CardId == seededCardId)
            .MinAsync(transaction => transaction.OccurredAt);
        var seededCard = await context.Cards.SingleAsync(card => card.Id == seededCardId);
        var seededTransaction = await context.Transactions.SingleAsync(transaction =>
            transaction.CardId == seededCardId
        );

        Assert.Equal("**** **** **** 4242", seededCard.MaskedPan);
        Assert.Equal(PrimaryVehicleId, seededCard.AssignedCarId);
        Assert.Equal(PrimaryVehicleId, seededTransaction.VehicleId);
        Assert.Equal("WA 12345", seededTransaction.VehiclePlate);
        Assert.True(seededEvent.EffectiveAt <= earliestSeededTransaction);
        Assert.Equal(TimeSpan.Zero, seededEvent.EffectiveAt.Offset);

        var seededHistory = await CreateService(context).GetStateHistoryAsync(seededCardId);
        var issuance = Assert.Single(seededHistory.History);
        Assert.Equal("Active", issuance.State);
        Assert.Equal(seededEvent.EffectiveAt, issuance.OccurredAt);
    }

    [Fact]
    public async Task Seeded_service_exposes_issued_cards_and_generated_transactions_from_one_store()
    {
        var service = new SeededFleetCardService(
            new SeededFleetVehicleService(),
            new TestCardProvider(),
            new MockFuelTransactionGenerator()
        );
        var baselineHistory = await service.GetStateHistoryAsync(SeededCardId);

        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "seeded-issue"
        );
        var issuedCardId = issue.Card!.Id;
        var beforeStateChanges = service
            .GetTransactionFacts()
            .Where(transaction => transaction.CardId == issuedCardId)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.VehicleId,
                transaction.VehiclePlate,
            })
            .OrderBy(transaction => transaction.Id)
            .ToArray();
        await service.ChangeStateAsync(
            issuedCardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Blocked)),
            "anna.nowak",
            "seeded-block"
        );
        await service.ChangeStateAsync(
            issuedCardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Stolen)),
            "anna.nowak",
            "seeded-stolen"
        );
        var cards = service.GetCards();
        var transactions = service
            .GetTransactions(from: null, to: null)
            .Where(transaction => transaction.CardId == issuedCardId)
            .ToArray();
        var afterStateChanges = service
            .GetTransactionFacts()
            .Where(transaction => transaction.CardId == issuedCardId)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.VehicleId,
                transaction.VehiclePlate,
            })
            .OrderBy(transaction => transaction.Id)
            .ToArray();
        var stateHistory = await service.GetStateHistoryAsync(issuedCardId);

        Assert.True(issue.IsSuccess);
        Assert.Equal("Active", Assert.Single(baselineHistory.History).State);
        Assert.Contains(cards, card => card.Id == issuedCardId);
        Assert.Equal(
            SecondVehicleId,
            Assert.Single(cards, card => card.Id == issuedCardId).AssignedCarId
        );
        Assert.Equal(beforeStateChanges, afterStateChanges);
        Assert.Equal(
            new[] { "Stolen", "Blocked", "Active" },
            stateHistory.History.Select(entry => entry.State)
        );
        Assert.InRange(transactions.Length, 1, 5);
        Assert.All(
            transactions,
            transaction =>
            {
                Assert.Equal(SecondVehicleId, transaction.VehicleId);
                Assert.Equal("PO 98765", transaction.VehiclePlate);
                Assert.Equal("95", transaction.FuelType);
                Assert.Equal(nameof(TransactionStatus.Completed), transaction.Status);
            }
        );
    }

    private static async Task<FleetCardsDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<FleetCardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
            )
            .Options;
        var context = new FleetCardsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static EfFleetCardService CreateService(
        FleetCardsDbContext context,
        ICardProvider? cardProvider = null
    ) => new(context, cardProvider ?? new TestCardProvider(), new MockFuelTransactionGenerator());

    private static object ToStableValues(FuelTransaction transaction) =>
        new
        {
            transaction.Id,
            transaction.OccurredAt,
            transaction.StationName,
            transaction.Quantity,
            transaction.UnitPrice,
            transaction.Discount,
            transaction.NetTotal,
            transaction.VatRate,
            transaction.VatAmount,
            transaction.GrossTotal,
        };

    private static FleetAuditEvent CreateAuditEvent(
        string id,
        string action,
        DateTimeOffset occurredAt
    ) =>
        new()
        {
            Id = Guid.Parse(id),
            Actor = "anna.nowak",
            Action = action,
            EntityType = "FleetCard",
            EntityId = Guid.NewGuid(),
            OccurredAt = occurredAt,
            Succeeded = true,
            CorrelationId = "state-history-test",
        };

    private sealed class TestCardProvider(Func<Task>? afterIssue = null) : ICardProvider
    {
        private int nextToken;

        public async Task<CardProviderIssueResult> IssueAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (afterIssue is not null)
            {
                await afterIssue();
            }

            var tokenNumber = Interlocked.Increment(ref nextToken);
            return new CardProviderIssueResult(
                "**** **** **** 4242",
                $"test-card-{tokenNumber}",
                new CardProviderMetadata("Test", "Virtual")
            );
        }
    }
}
