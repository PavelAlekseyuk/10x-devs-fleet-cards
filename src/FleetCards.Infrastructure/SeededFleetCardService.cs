using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Infrastructure;

public sealed class SeededFleetCardService(
    SeededFleetVehicleService vehicleService,
    ICardProvider cardProvider,
    MockFuelTransactionGenerator transactionGenerator
) : IFleetCardService
{
    private static readonly Guid ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeededCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SeededVehicleId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly DateTimeOffset SeededAssignmentTime = new(
        2026,
        9,
        9,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    private readonly object sync = new();
    private readonly List<FleetCard> cards =
    [
        new FleetCard
        {
            Id = SeededCardId,
            ClientId = ClientId,
            MaskedPan = "**** **** **** 4242",
            ProviderToken = "mock-card-4242",
            State = CardState.Active,
            AssignedCarId = SeededVehicleId,
        },
    ];
    private readonly List<FleetCardAssignmentEvent> assignmentEvents =
    [
        new FleetCardAssignmentEvent
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CardId = SeededCardId,
            VehicleId = SeededVehicleId,
            EffectiveAt = SeededAssignmentTime,
            OrderingKey = 1,
        },
    ];
    private readonly List<FuelTransaction> transactions =
    [
        new FuelTransaction
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            LineageId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Version = 1,
            CardId = SeededCardId,
            VehicleId = SeededVehicleId,
            VehiclePlate = "WA 12345",
            OccurredAt = SeededAssignmentTime,
            PosOccurredAtLocal = new DateTimeOffset(2026, 9, 9, 14, 0, 0, TimeSpan.FromHours(2)),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "angry storage",
            FuelType = "Diesel",
            Quantity = 42.125m,
            UnitPrice = 6.49m,
            Discount = 0.18m,
            NetTotal = 265.53m,
            VatRate = MockFuelTransactionGenerator.DefaultVatRate,
            VatAmount = 61.07m,
            GrossTotal = 326.60m,
            Status = TransactionStatus.Completed,
        },
    ];
    private readonly List<FleetAuditEvent> auditEvents = [];

    public IReadOnlyList<FleetCardResponse> GetCards()
    {
        lock (sync)
        {
            return cards.OrderBy(card => card.MaskedPan).Select(ToResponse).ToArray();
        }
    }

    public IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    )
    {
        lock (sync)
        {
            return transactions
                .Where(transaction => from is null || transaction.OccurredAt >= from)
                .Where(transaction => to is null || transaction.OccurredAt <= to)
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Select(transaction => new FuelTransactionResponse(
                    transaction.Id,
                    transaction.CardId,
                    transaction.VehicleId,
                    transaction.VehiclePlate,
                    transaction.OccurredAt,
                    transaction.StationName,
                    transaction.FuelType,
                    transaction.Quantity,
                    transaction.UnitPrice,
                    transaction.Discount,
                    transaction.NetTotal,
                    transaction.VatRate,
                    transaction.VatAmount,
                    transaction.GrossTotal,
                    transaction.Status.ToString()
                ))
                .ToArray();
        }
    }

    public async Task<CardCommandResult> IssueAsync(
        IssueFleetCardRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var vehicle = await FindVehicleAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return CardCommandResult.NotFound();
        }

        if (vehicle.IsArchived)
        {
            return CardCommandResult.Conflict();
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var providerResult = await cardProvider.IssueAsync(cancellationToken);
        var card = new FleetCard
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            MaskedPan = providerResult.MaskedPan,
            ProviderToken = providerResult.ProviderToken,
            State = CardState.Active,
            AssignedCarId = vehicle.Id,
        };
        var generatedTransactions = transactionGenerator.Generate(card.Id, vehicle, issuedAt);

        lock (sync)
        {
            cards.Add(card);
            assignmentEvents.Add(
                new FleetCardAssignmentEvent
                {
                    Id = Guid.NewGuid(),
                    CardId = card.Id,
                    VehicleId = vehicle.Id,
                    EffectiveAt = issuedAt,
                    OrderingKey = 1,
                }
            );
            transactions.AddRange(generatedTransactions);
            AddAudit(card.Id, "CardIssued", actor, correlationId, issuedAt);
        }

        return CardCommandResult.Success(ToResponse(card));
    }

    public Task<CardCommandResult> ChangeStateAsync(
        Guid cardId,
        ChangeFleetCardStateRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        if (!CardStateRules.TryParse(request.State, out var state))
        {
            return Task.FromResult(
                CardCommandResult.Invalid(
                    new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(ChangeFleetCardStateRequest.State)] =
                        [
                            "State must be a defined card state name.",
                        ],
                    }
                )
            );
        }

        lock (sync)
        {
            var card = cards.SingleOrDefault(candidate => candidate.Id == cardId);
            if (card is null)
            {
                return Task.FromResult(CardCommandResult.NotFound());
            }

            card.State = state;
            AddAudit(
                card.Id,
                $"CardStateChangedTo{state}",
                actor,
                correlationId,
                DateTimeOffset.UtcNow
            );
            return Task.FromResult(CardCommandResult.Success(ToResponse(card)));
        }
    }

    public async Task<CardCommandResult> ReassignAsync(
        Guid cardId,
        ReassignFleetCardRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var vehicle = await FindVehicleAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return CardCommandResult.NotFound();
        }

        if (vehicle.IsArchived)
        {
            return CardCommandResult.Conflict();
        }

        lock (sync)
        {
            var card = cards.SingleOrDefault(candidate => candidate.Id == cardId);
            if (card is null)
            {
                return CardCommandResult.NotFound();
            }

            if (card.State == CardState.Archived || card.AssignedCarId == vehicle.Id)
            {
                return CardCommandResult.Conflict();
            }

            var previous = assignmentEvents
                .Where(assignment => assignment.CardId == card.Id)
                .OrderByDescending(assignment => assignment.EffectiveAt)
                .ThenByDescending(assignment => assignment.OrderingKey)
                .First();
            var occurredAt = DateTimeOffset.UtcNow;
            if (previous.EffectiveAt > occurredAt)
            {
                occurredAt = previous.EffectiveAt;
            }

            card.AssignedCarId = vehicle.Id;
            assignmentEvents.Add(
                new FleetCardAssignmentEvent
                {
                    Id = Guid.NewGuid(),
                    CardId = card.Id,
                    VehicleId = vehicle.Id,
                    EffectiveAt = occurredAt,
                    OrderingKey = previous.OrderingKey + 1,
                }
            );
            AddAudit(card.Id, "CardReassigned", actor, correlationId, occurredAt);
            return CardCommandResult.Success(ToResponse(card));
        }
    }

    public async Task<CardAssignmentHistoryResult> GetAssignmentHistoryAsync(
        Guid cardId,
        CancellationToken cancellationToken = default
    )
    {
        var vehicles = await vehicleService.GetCarsAsync(
            search: null,
            includeArchived: true,
            cancellationToken: cancellationToken
        );
        var vehiclesById = vehicles.ToDictionary(vehicle => vehicle.Id);
        FleetCardAssignmentEvent[] assignments;
        lock (sync)
        {
            if (!cards.Any(card => card.Id == cardId))
            {
                return CardAssignmentHistoryResult.NotFound();
            }

            assignments = assignmentEvents
                .Where(assignment => assignment.CardId == cardId)
                .OrderBy(assignment => assignment.EffectiveAt)
                .ThenBy(assignment => assignment.OrderingKey)
                .ToArray();
        }

        var history = assignments
            .Select(
                (assignment, index) =>
                    new FleetCardAssignmentHistoryResponse(
                        assignment.CardId,
                        assignment.VehicleId,
                        vehiclesById[assignment.VehicleId].RegistrationPlate,
                        assignment.EffectiveAt,
                        index + 1 < assignments.Length ? assignments[index + 1].EffectiveAt : null,
                        assignment.OrderingKey
                    )
            )
            .ToArray();
        return CardAssignmentHistoryResult.Success(history);
    }

    private async Task<FleetCar?> FindVehicleAsync(
        Guid vehicleId,
        CancellationToken cancellationToken
    )
    {
        var vehicle = (
            await vehicleService.GetCarsAsync(
                search: null,
                includeArchived: true,
                cancellationToken: cancellationToken
            )
        ).SingleOrDefault(candidate => candidate.Id == vehicleId);
        return vehicle is null
            ? null
            : new FleetCar
            {
                Id = vehicle.Id,
                ClientId = ClientId,
                RegistrationPlate = vehicle.RegistrationPlate,
                NormalizedRegistrationPlate = FleetVehicleRules.NormalizeRegistrationPlate(
                    vehicle.RegistrationPlate
                ),
                FuelType = vehicle.FuelType,
                Make = vehicle.Make,
                Model = vehicle.Model,
                IsArchived = vehicle.IsArchived,
            };
    }

    private void AddAudit(
        Guid cardId,
        string action,
        string actor,
        string correlationId,
        DateTimeOffset occurredAt
    ) =>
        auditEvents.Add(
            new FleetAuditEvent
            {
                Id = Guid.NewGuid(),
                Actor = actor,
                Action = action,
                EntityType = "FleetCard",
                EntityId = cardId,
                OccurredAt = occurredAt,
                Succeeded = true,
                CorrelationId = correlationId,
            }
        );

    private static FleetCardResponse ToResponse(FleetCard card) =>
        new(card.Id, card.MaskedPan, card.ProviderToken, card.State.ToString(), card.AssignedCarId);
}
