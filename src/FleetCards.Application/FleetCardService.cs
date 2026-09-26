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

public sealed record CardAssignmentHistoryResult(
    IReadOnlyList<FleetCardAssignmentHistoryResponse> Assignments,
    CardCommandError Error
)
{
    public static CardAssignmentHistoryResult Success(
        IReadOnlyList<FleetCardAssignmentHistoryResponse> assignments
    ) => new(assignments, CardCommandError.None);

    public static CardAssignmentHistoryResult NotFound() =>
        new([], CardCommandError.NotFound);
}

public interface IFleetCardService
{
    IReadOnlyList<FleetCardResponse> GetCards();
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

    Task<CardCommandResult> ReassignAsync(
        Guid cardId,
        ReassignFleetCardRequest request,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<CardAssignmentHistoryResult> GetAssignmentHistoryAsync(
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