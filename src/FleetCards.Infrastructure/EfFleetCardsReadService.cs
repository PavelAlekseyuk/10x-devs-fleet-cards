using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class EfFleetCardsReadService(FleetCardsDbContext dbContext) : IFleetCardsReadService
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

    public IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    )
    {
        var query = dbContext.Transactions.AsNoTracking().AsQueryable();
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

    public IReadOnlyList<FleetCardProgramResponse> GetPrograms() =>
        dbContext
            .Programs.AsNoTracking()
            .OrderBy(program => program.ActiveFrom)
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
