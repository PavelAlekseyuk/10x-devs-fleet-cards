using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Application;

public enum CardCommandError
{
    None,
    Validation,
    NotFound,
    Conflict,
}

public sealed record CardCommandResult(
    FleetCardResponse? Card,
    CardCommandError Error,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
)
{
    public bool IsSuccess => Error == CardCommandError.None;

    public static CardCommandResult Success(FleetCardResponse card) =>
        new(card, CardCommandError.None);

    public static CardCommandResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, CardCommandError.Validation, errors);

    public static CardCommandResult NotFound() => new(null, CardCommandError.NotFound);

    public static CardCommandResult Conflict() => new(null, CardCommandError.Conflict);
}

public sealed record CardProviderMetadata(string Provider, string Product);

public sealed record CardProviderIssueResult(
    string MaskedPan,
    string ProviderToken,
    CardProviderMetadata Metadata
);

public interface ICardProvider
{
    Task<CardProviderIssueResult> IssueAsync(CancellationToken cancellationToken = default);
}

public sealed record CardStateHistoryResult(
    IReadOnlyList<FleetCardStateHistoryResponse> History,
    CardCommandError Error
)
{
    public static CardStateHistoryResult Success(
        IReadOnlyList<FleetCardStateHistoryResponse> history
    ) => new(history, CardCommandError.None);

    public static CardStateHistoryResult NotFound() => new([], CardCommandError.NotFound);
}

public static class CardStateHistoryProjection
{
    private const string StateChangePrefix = "CardStateChangedTo";

    public static IReadOnlyList<FleetCardStateHistoryResponse> Project(
        IEnumerable<FleetAuditEvent> auditEvents
    ) =>
        auditEvents
            .Where(auditEvent => auditEvent.EntityType == "FleetCard" && auditEvent.Succeeded)
            .Select(auditEvent => (AuditEvent: auditEvent, State: GetState(auditEvent.Action)))
            .Where(entry => entry.State is not null)
            .OrderByDescending(entry => entry.AuditEvent.OccurredAt)
            .ThenByDescending(entry => entry.AuditEvent.Id)
            .Select(entry => new FleetCardStateHistoryResponse(
                entry.State!,
                entry.AuditEvent.OccurredAt
            ))
            .ToArray();

    private static string? GetState(string action)
    {
        if (action == "CardIssued")
        {
            return nameof(CardState.Active);
        }

        return
            action.StartsWith(StateChangePrefix, StringComparison.Ordinal)
            && CardStateRules.TryParse(action[StateChangePrefix.Length..], out var state)
            ? state.ToString()
            : null;
    }
}

public interface IFleetCardService
{
    IReadOnlyList<FleetCardResponse> GetCards();
    IReadOnlyList<FuelTransaction> GetTransactionFacts();
    IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    );

    Task<CardCommandResult> IssueAsync(
        IssueFleetCardRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<CardCommandResult> ChangeStateAsync(
        Guid cardId,
        ChangeFleetCardStateRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<CardStateHistoryResult> GetStateHistoryAsync(
        Guid cardId,
        CancellationToken cancellationToken = default
    );
}

public static class CardStateRules
{
    public static bool TryParse(string? value, out CardState state)
    {
        foreach (var name in Enum.GetNames<CardState>())
        {
            if (string.Equals(name, value, StringComparison.Ordinal))
            {
                state = Enum.Parse<CardState>(name, ignoreCase: false);
                return true;
            }
        }

        state = default;
        return false;
    }
}
