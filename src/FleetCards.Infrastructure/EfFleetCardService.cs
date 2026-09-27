using System.Data;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class EfFleetCardService(
    FleetCardsDbContext dbContext,
    ICardProvider cardProvider,
    MockFuelTransactionGenerator transactionGenerator
) : IFleetCardService
{
    public IReadOnlyList<FleetCardResponse> GetCards() =>
        dbContext
            .Cards.AsNoTracking()
            .OrderBy(card => card.MaskedPan)
            .Select(card => new FleetCardResponse(
                card.Id,
                card.MaskedPan,
                card.ProviderToken,
                card.State.ToString(),
                card.AssignedCarId
            ))
            .ToArray();

    public IReadOnlyList<FuelTransaction> GetTransactionFacts() =>
        dbContext.Transactions.AsNoTracking().ToArray();

    public IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    )
    {
        IQueryable<FuelTransaction> query = dbContext.Transactions.AsNoTracking();
        if (from is not null)
        {
            query = query.Where(transaction => transaction.OccurredAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(transaction => transaction.OccurredAt <= to);
        }

        return query
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

    public async Task<CardCommandResult> IssueAsync(
        IssueFleetCardRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var initialVehicle = await dbContext
            .Cars.AsNoTracking()
            .SingleOrDefaultAsync(car => car.Id == request.VehicleId, cancellationToken);
        if (initialVehicle is null)
        {
            return CardCommandResult.NotFound();
        }

        if (initialVehicle.IsArchived)
        {
            return CardCommandResult.Conflict();
        }

        var providerResult = await cardProvider.IssueAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var vehicle = await dbContext.Cars.SingleOrDefaultAsync(
            car => car.Id == request.VehicleId,
            cancellationToken
        );
        if (vehicle is null)
        {
            return CardCommandResult.NotFound();
        }

        if (vehicle.IsArchived)
        {
            return CardCommandResult.Conflict();
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var card = new FleetCard
        {
            Id = Guid.NewGuid(),
            ClientId = vehicle.ClientId,
            MaskedPan = providerResult.MaskedPan,
            ProviderToken = providerResult.ProviderToken,
            State = CardState.Active,
            AssignedCarId = vehicle.Id,
        };
        var assignment = new FleetCardAssignmentEvent
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            VehicleId = vehicle.Id,
            EffectiveAt = issuedAt,
            OrderingKey = 1,
        };
        var transactions = transactionGenerator.Generate(card.Id, vehicle, issuedAt);

        dbContext.Cards.Add(card);
        dbContext.CardAssignmentEvents.Add(assignment);
        dbContext.Transactions.AddRange(transactions);
        dbContext.AuditEvents.Add(
            CreateAudit(card.Id, "CardIssued", actor, correlationId, issuedAt)
        );
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsAssignmentWriteConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            return CardCommandResult.Conflict();
        }

        return CardCommandResult.Success(ToResponse(card));
    }

    public async Task<CardCommandResult> ChangeStateAsync(
        Guid cardId,
        ChangeFleetCardStateRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        if (!CardStateRules.TryParse(request.State, out var state))
        {
            return CardCommandResult.Invalid(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(ChangeFleetCardStateRequest.State)] =
                    [
                        "State must be a defined card state name.",
                    ],
                }
            );
        }

        var card = await dbContext.Cards.SingleOrDefaultAsync(
            candidate => candidate.Id == cardId,
            cancellationToken
        );
        if (card is null)
        {
            return CardCommandResult.NotFound();
        }

        card.State = state;
        var occurredAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(
            CreateAudit(card.Id, $"CardStateChangedTo{state}", actor, correlationId, occurredAt)
        );
        await dbContext.SaveChangesAsync(cancellationToken);
        return CardCommandResult.Success(ToResponse(card));
    }

    public async Task<CardStateHistoryResult> GetStateHistoryAsync(
        Guid cardId,
        CancellationToken cancellationToken = default
    )
    {
        if (!await dbContext.Cards.AnyAsync(card => card.Id == cardId, cancellationToken))
        {
            return CardStateHistoryResult.NotFound();
        }

        var auditEvents = await dbContext
            .AuditEvents.AsNoTracking()
            .Where(auditEvent =>
                auditEvent.EntityType == "FleetCard"
                && auditEvent.EntityId == cardId
                && auditEvent.Succeeded
            )
            .ToArrayAsync(cancellationToken);
        return CardStateHistoryResult.Success(CardStateHistoryProjection.Project(auditEvents));
    }

    private static FleetCardResponse ToResponse(FleetCard card) =>
        new(card.Id, card.MaskedPan, card.ProviderToken, card.State.ToString(), card.AssignedCarId);

    private static bool IsAssignmentWriteConflict(Exception exception)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (
                cause is SqlException sqlException
                && (
                    sqlException.Number == 1205
                    || (
                        sqlException.Number is 2601 or 2627
                        && sqlException.Message.Contains(
                            "IX_CardAssignmentEvents_CardId_OrderingKey",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
            )
            {
                return true;
            }
        }

        return exception is DbUpdateConcurrencyException;
    }

    private static FleetAuditEvent CreateAudit(
        Guid cardId,
        string action,
        string actor,
        string correlationId,
        DateTimeOffset occurredAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Actor = actor,
            Action = action,
            EntityType = "FleetCard",
            EntityId = cardId,
            OccurredAt = occurredAt,
            Succeeded = true,
            CorrelationId = correlationId,
        };
}
