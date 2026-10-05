using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class EfFleetCardsReadService(
    FleetCardsDbContext dbContext,
    IFleetCardService cardService,
    ITransactionReportingService reportingService
) : IFleetCardsReadService
{
    public CurrentUserResponse GetCurrentUser()
    {
        var user = dbContext.Users.AsNoTracking().Single(user => user.UserName == "anna.nowak");
        return new CurrentUserResponse(user.UserName, user.Permission);
    }

    public ClientResponse GetClient()
    {
        var client = dbContext.Clients.AsNoTracking().Single();
        return new ClientResponse(
            client.LegalName,
            client.Nip,
            client.Jurisdiction,
            client.Address
        );
    }

    public IReadOnlyList<FleetCardResponse> GetCards() => cardService.GetCards();

    public async Task<TransactionReportResponse> GetTransactionReportAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    )
    {
        var transactions = await GetReportTransactionsAsync(period, cancellationToken);
        return reportingService.CreateReport(period, transactions);
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    )
    {
        var transactions = await GetReportTransactionsAsync(period, cancellationToken);
        var activeVehicleCount = await dbContext
            .Cars.AsNoTracking()
            .CountAsync(car => !car.IsArchived, cancellationToken);
        var activeCardCount = await dbContext
            .Cards.AsNoTracking()
            .CountAsync(card => card.State == CardState.Active, cancellationToken);
        return reportingService.CreateSummary(
            period,
            transactions,
            activeVehicleCount,
            activeCardCount
        );
    }

    private async Task<IReadOnlyList<FuelTransaction>> GetReportTransactionsAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken
    )
    {
        var lineageIds = await dbContext
            .Transactions.AsNoTracking()
            .Where(transaction =>
                transaction.OccurredAt >= period.StartUtc && transaction.OccurredAt < period.EndUtc
            )
            .Select(transaction => transaction.LineageId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (lineageIds.Length == 0)
        {
            return [];
        }

        return await dbContext
            .Transactions.AsNoTracking()
            .Where(transaction =>
                lineageIds.Contains(transaction.LineageId)
                && (
                    transaction.Status == TransactionStatus.Completed
                    || transaction.Status == TransactionStatus.Recalculated
                    || (
                        transaction.OccurredAt >= period.StartUtc
                        && transaction.OccurredAt < period.EndUtc
                    )
                )
            )
            .OrderBy(transaction => transaction.LineageId)
            .ThenByDescending(transaction => transaction.Version)
            .ThenBy(transaction => transaction.Id)
            .ToArrayAsync(cancellationToken);
    }

    public IReadOnlyList<FleetCardProgramResponse> GetPrograms() =>
        dbContext
            .Programs.AsNoTracking()
            .OrderBy(program => program.ActiveFrom)
            .ThenBy(program => program.Name)
            .Select(program => new FleetCardProgramResponse(
                program.Id,
                program.Name,
                program.ActiveFrom,
                program.ActiveTo
            ))
            .ToArray();
}

public sealed class EfAuthenticationService(FleetCardsDbContext dbContext) : IAuthenticationService
{
    public CurrentUserResponse? ValidateCredentials(string userName, string password)
    {
        var user = FindUser(userName);

        return user is not null && CredentialHashing.Verify(password, user.PasswordHash)
            ? new CurrentUserResponse(user.UserName, user.Permission)
            : null;
    }

    public CurrentUserResponse? GetCurrentUser(string userName)
    {
        var user = FindUser(userName);
        return user is null ? null : new CurrentUserResponse(user.UserName, user.Permission);
    }

    private FleetUser? FindUser(string userName) =>
        dbContext.Users.AsNoTracking().SingleOrDefault(user => user.UserName == userName.Trim());
}

public sealed class EfProgramResolutionDataSource(FleetCardsDbContext dbContext)
    : IProgramResolutionDataSource
{
    public IReadOnlyList<FleetCardProgramRule> GetRules() =>
        dbContext.ProgramRules.AsNoTracking().ToArray();

    public IReadOnlyList<ClientProgramAssignment> GetAssignments() =>
        dbContext.ClientProgramAssignments.AsNoTracking().ToArray();
}
