using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Application;

public interface IAuthenticationService
{
    CurrentUserResponse? ValidateCredentials(string userName, string password);
    CurrentUserResponse? GetCurrentUser(string userName);
}

public static class CredentialHashing
{
    private const int SaltLength = 16;
    private const int KeyLength = 32;
    private const int Iterations = 120_000;
    private const string Algorithm = "pbkdf2-sha256";

    public static bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split(':');
        if (
            parts.Length != 5
            || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out var iterations)
            || iterations < 1
        )
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expectedKey = Convert.FromBase64String(parts[4]);
            var actualKey = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                expectedKey.Length
            );

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                actualKey,
                expectedKey
            );
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class SeededAuthenticationService : IAuthenticationService
{
    private static readonly FleetUser[] Users =
    [
        new()
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            UserName = "anna.nowak",
            PasswordHash =
                "pbkdf2-sha256:120000:32:YW5uYS5ub3dha3xmbGVldGNhcmRzfHYx:ii5CUBhy/r7ijfx9rxha1fx84ADBy5blrUopRVSUQOE=",
            Permission = "ReadWrite",
        },
        new()
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            UserName = "piotr.wisniewski",
            PasswordHash =
                "pbkdf2-sha256:120000:32:cGlvdHIud2lzbmlld3NraXxmbGVldGNhcmRzfHYx:Jey8SEvmKOAL0FlmRyh51nW+zGLSsICzEqjEb9w9bgQ=",
            Permission = "ReadOnly",
        },
    ];

    public CurrentUserResponse? ValidateCredentials(string userName, string password)
    {
        var user = Users.SingleOrDefault(user =>
            string.Equals(user.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase)
        );

        return user is not null && CredentialHashing.Verify(password, user.PasswordHash)
            ? new CurrentUserResponse(user.UserName, user.Permission)
            : null;
    }

    public CurrentUserResponse? GetCurrentUser(string userName)
    {
        var user = Users.SingleOrDefault(user =>
            string.Equals(user.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase)
        );

        return user is null ? null : new CurrentUserResponse(user.UserName, user.Permission);
    }
}

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
            new FleetCarResponse(carId, "WA 12345", "Diesel", "Volvo", "FH", false),
            new FleetCarResponse(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "PO 98765",
                "95",
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
