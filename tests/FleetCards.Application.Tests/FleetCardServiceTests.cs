using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FleetCards.Application.Tests;

public sealed class FleetCardServiceTests
{
    private static readonly Guid PrimaryVehicleId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly Guid SecondVehicleId = Guid.Parse(
        "33333333-3333-3333-3333-333333333333"
    );

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

        var transactions = await context.Transactions
            .Where(transaction => transaction.CardId == card.Id)
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
        Assert.All(transactions, transaction =>
        {
            Assert.Equal(PrimaryVehicleId, transaction.VehicleId);
            Assert.Equal("WA 12345", transaction.VehiclePlate);
            Assert.Equal("Diesel", transaction.FuelType);
            Assert.Equal(TransactionStatus.Completed, transaction.Status);
            Assert.InRange(transaction.OccurredAt, previousMonthStart, currentMonthStart.AddTicks(-1));
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
        });

        var assignments = await context.CardAssignmentEvents
            .Where(assignment => assignment.CardId == card.Id)
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
        var archivedVehicleId = await context.Cars
            .Where(vehicle => vehicle.IsArchived)
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
        var audits = await context.AuditEvents
            .Where(auditEvent => auditEvent.EntityId == cardId)
            .ToArrayAsync();

        Assert.Equal(CardCommandError.Validation, invalid.Error);
        Assert.All(Enum.GetNames<CardState>(), state =>
            Assert.Contains(audits, auditEvent => auditEvent.Action == $"CardStateChangedTo{state}")
        );
    }

    [Fact]
    public async Task Reassignment_appends_periods_and_keeps_transaction_vehicle_snapshots()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-reassignment-card"
        );
        var cardId = issue.Card!.Id;
        var before = await context.Transactions
            .Where(transaction => transaction.CardId == cardId)
            .Select(transaction => new { transaction.Id, transaction.VehicleId, transaction.VehiclePlate })
            .ToArrayAsync();

        var reassignment = await service.ReassignAsync(
            cardId,
            new ReassignFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "reassign-card"
        );
        var history = await service.GetAssignmentHistoryAsync(cardId);
        var after = await context.Transactions
            .Where(transaction => transaction.CardId == cardId)
            .Select(transaction => new { transaction.Id, transaction.VehicleId, transaction.VehiclePlate })
            .ToArrayAsync();

        Assert.True(reassignment.IsSuccess);
        Assert.Equal(SecondVehicleId, reassignment.Card!.AssignedCarId);
        Assert.Equal(before, after);
        Assert.True(history.Assignments.Count >= 2);
        var current = history.Assignments[^1];
        Assert.Equal(SecondVehicleId, current.VehicleId);
        Assert.Equal("PO 98765", current.VehicleRegistrationPlate);
        Assert.Null(current.EffectiveTo);
        Assert.Equal(2, current.OrderingKey);
        Assert.Equal(current.EffectiveFrom, history.Assignments[^2].EffectiveTo);
        Assert.Equal(1, history.Assignments[^2].OrderingKey);

        var archived = await context.Cars.SingleAsync(vehicle => vehicle.Id == SecondVehicleId);
        archived.IsArchived = true;
        await context.SaveChangesAsync();
        var archivedTarget = await service.ReassignAsync(
            cardId,
            new ReassignFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "reassign-archived"
        );
        Assert.Equal(CardCommandError.Conflict, archivedTarget.Error);
    }

    [Fact]
    public async Task Archived_card_cannot_be_reassigned_until_reactivated()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-archived-card"
        );
        var cardId = issue.Card!.Id;
        await service.ChangeStateAsync(
            cardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Archived)),
            "anna.nowak",
            "archive-card"
        );

        var rejected = await service.ReassignAsync(
            cardId,
            new ReassignFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "reassign-archived-card"
        );
        await service.ChangeStateAsync(
            cardId,
            new ChangeFleetCardStateRequest(nameof(CardState.Active)),
            "anna.nowak",
            "reactivate-card"
        );
        var accepted = await service.ReassignAsync(
            cardId,
            new ReassignFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "reassign-reactivated-card"
        );

        Assert.Equal(CardCommandError.Conflict, rejected.Error);
        Assert.True(accepted.IsSuccess);
    }

    [Fact]
    public async Task Assignment_history_uses_ordering_key_when_effective_timestamps_match()
    {
        await using var context = await CreateContextAsync();
        var service = CreateService(context);
        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(PrimaryVehicleId),
            "anna.nowak",
            "issue-equal-time-card"
        );
        var cardId = issue.Card!.Id;
        var firstAssignment = await context.CardAssignmentEvents.SingleAsync(
            assignment => assignment.CardId == cardId
        );
        var effectiveAt = firstAssignment.EffectiveAt;
        context.Cards.Single(card => card.Id == cardId).AssignedCarId = SecondVehicleId;
        context.CardAssignmentEvents.Add(
            new FleetCardAssignmentEvent
            {
                Id = Guid.NewGuid(),
                CardId = cardId,
                VehicleId = SecondVehicleId,
                EffectiveAt = effectiveAt,
                OrderingKey = 2,
            }
        );
        await context.SaveChangesAsync();

        var history = await service.GetAssignmentHistoryAsync(cardId);

        Assert.Equal(
            new long[] { 1, 2 },
            history.Assignments.Select(assignment => assignment.OrderingKey)
        );
        Assert.Equal(effectiveAt, history.Assignments[0].EffectiveTo);
        Assert.Null(history.Assignments[1].EffectiveTo);
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
        var previousMonthStart = new DateTimeOffset(
            2026,
            8,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        );
        var currentMonthStart = previousMonthStart.AddMonths(1);

        Assert.InRange(first.Count, 1, 5);
        Assert.Equal(first.Select(ToStableValues), second.Select(ToStableValues));
        Assert.All(first, transaction =>
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
        });
    }

    [Fact]
    public async Task Ef_model_enforces_unique_per_card_order_and_seeds_initial_assignment()
    {
        await using var context = await CreateContextAsync();

        var entityType = context.Model.FindEntityType(typeof(FleetCardAssignmentEvent))!;
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(FleetCardAssignmentEvent.CardId), nameof(FleetCardAssignmentEvent.OrderingKey)])
        );
        var seededCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var seededEvent = await context.CardAssignmentEvents.SingleAsync(
            assignment => assignment.CardId == seededCardId
        );
        var earliestSeededTransaction = await context.Transactions
            .Where(transaction => transaction.CardId == seededCardId)
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
    }

    [Fact]
    public async Task Seeded_service_exposes_issued_cards_and_generated_transactions_from_one_store()
    {
        var service = new SeededFleetCardService(
            new SeededFleetVehicleService(),
            new TestCardProvider(),
            new MockFuelTransactionGenerator()
        );

        var issue = await service.IssueAsync(
            new IssueFleetCardRequest(SecondVehicleId),
            "anna.nowak",
            "seeded-issue"
        );
        var cards = service.GetCards();
        var transactions = service
            .GetTransactions(from: null, to: null)
            .Where(transaction => transaction.CardId == issue.Card!.Id)
            .ToArray();

        Assert.True(issue.IsSuccess);
        Assert.Contains(cards, card => card.Id == issue.Card!.Id);
        Assert.InRange(transactions.Length, 1, 5);
        Assert.All(transactions, transaction =>
        {
            Assert.Equal(SecondVehicleId, transaction.VehicleId);
            Assert.Equal("PO 98765", transaction.VehiclePlate);
            Assert.Equal("95", transaction.FuelType);
            Assert.Equal(nameof(TransactionStatus.Completed), transaction.Status);
        });
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

    private static EfFleetCardService CreateService(FleetCardsDbContext context) =>
        new(context, new TestCardProvider(), new MockFuelTransactionGenerator());

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

    private sealed class TestCardProvider : ICardProvider
    {
        private int nextToken;

        public Task<CardProviderIssueResult> IssueAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tokenNumber = Interlocked.Increment(ref nextToken);
            return Task.FromResult(
                new CardProviderIssueResult(
                    "**** **** **** 4242",
                    $"test-card-{tokenNumber}",
                    new CardProviderMetadata("Test", "Virtual")
                )
            );
        }
    }
}