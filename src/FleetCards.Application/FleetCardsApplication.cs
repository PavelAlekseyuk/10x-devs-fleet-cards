using FleetCards.Contracts;

namespace FleetCards.Application;

public interface IFleetCardsReadService
{
    CurrentUserResponse GetCurrentUser();
    ClientResponse GetClient();
    IReadOnlyList<FleetCarResponse> GetCars(string? search);
    IReadOnlyList<FleetCardResponse> GetCards();
    IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    );
    IReadOnlyList<FleetCardProgramResponse> GetPrograms();
}

public sealed class SeededFleetCardsReadService : IFleetCardsReadService
{
    private readonly Guid carId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid cardId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public CurrentUserResponse GetCurrentUser() => new("anna.nowak", "ReadWrite");

    public ClientResponse GetClient() =>
        new(
            "Baltic Logistics Sp. z o.o.",
            "5250001234",
            "Poland",
            "ul. Prosta 12, 00-850 Warszawa"
        );

    public IReadOnlyList<FleetCarResponse> GetCars(string? search)
    {
        var cars = new[]
        {
            new FleetCarResponse(carId, "WA 12345", "Volvo", "FH", false),
            new FleetCarResponse(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "PO 98765",
                "Toyota",
                "Corolla",
                false
            ),
        };

        return string.IsNullOrWhiteSpace(search)
            ? cars
            : cars.Where(car =>
                    car.RegistrationPlate.Contains(
                        search.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToArray();
    }

    public IReadOnlyList<FleetCardResponse> GetCards() =>
        [new(cardId, "**** **** **** 4242", "mock-card-4242", "Active", carId)];

    public IReadOnlyList<FuelTransactionResponse> GetTransactions(
        DateTimeOffset? from,
        DateTimeOffset? to
    )
    {
        var transaction = new FuelTransactionResponse(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            cardId,
            carId,
            "WA 12345",
            DateTimeOffset.UtcNow.AddDays(-12),
            "angry storage",
            "Diesel",
            42.125m,
            6.49m,
            0.18m,
            265.53m,
            0.23m,
            61.07m,
            326.60m,
            "Completed"
        );

        return
            (from is null || transaction.OccurredAt >= from)
            && (to is null || transaction.OccurredAt <= to)
            ? [transaction]
            : [];
    }

    public IReadOnlyList<FleetCardProgramResponse> GetPrograms() =>
        [
            new(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "Baltic Standard",
                DateTimeOffset.UtcNow.AddMonths(-6),
                null
            ),
        ];
}
