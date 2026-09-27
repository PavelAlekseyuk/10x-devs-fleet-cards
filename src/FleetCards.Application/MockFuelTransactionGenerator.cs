using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FleetCards.Domain;

namespace FleetCards.Application;

public sealed class MockFuelTransactionGenerator
{
    public const decimal DefaultVatRate = 0.23m;
    private const string PointOfSaleTimeZoneId = "Europe/Warsaw";
    private static readonly TimeZoneInfo PointOfSaleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        PointOfSaleTimeZoneId
    );

    private static readonly string[] StationNames =
    [
        "Northway Fuel",
        "City Petrol",
        "Baltic Energy",
        "Roadside Market",
        "Central Station",
    ];

    private readonly decimal vatRate;

    public MockFuelTransactionGenerator(decimal vatRate = DefaultVatRate)
    {
        if (vatRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        }

        this.vatRate = vatRate;
    }

    public IReadOnlyList<FuelTransaction> Generate(
        Guid cardId,
        FleetCar vehicle,
        DateTimeOffset issuedAt
    )
    {
        var issuedAtUtc = issuedAt.ToUniversalTime();
        var issuedAtLocal = TimeZoneInfo.ConvertTime(issuedAtUtc, PointOfSaleTimeZone);
        var currentLocalMonthStart = new DateTime(
            issuedAtLocal.Year,
            issuedAtLocal.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified
        );
        var previousLocalMonthStart = currentLocalMonthStart.AddMonths(-1);
        var previousMonthStart = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(previousLocalMonthStart, PointOfSaleTimeZone)
        );
        var currentMonthStart = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(currentLocalMonthStart, PointOfSaleTimeZone)
        );
        var random = CreateRandom(cardId, vehicle.Id, issuedAtUtc);
        var count = random.Next(5, 16);
        var transactions = new FuelTransaction[count];
        var monthDurationMinutes = (int)(currentMonthStart - previousMonthStart).TotalMinutes;

        for (var index = 0; index < count; index++)
        {
            var quantity = random.Next(15_000, 55_001) / 1_000m;
            var unitPrice = random.Next(450, 801) / 100m;
            var discount = random.Next(5, 31) / 100m;
            var occurredAt = previousMonthStart.AddMinutes(random.Next(0, monthDurationMinutes));
            var netTotal = TransactionReportingService.CalculateNetTotal(
                quantity,
                unitPrice,
                discount
            );
            var vatAmount = TransactionReportingService.CalculateVatAmount(netTotal, vatRate);

            transactions[index] = new FuelTransaction
            {
                Id = CreateTransactionId(cardId, vehicle.Id, issuedAtUtc, index),
                LineageId = CreateTransactionId(cardId, vehicle.Id, issuedAtUtc, index),
                Version = 1,
                CardId = cardId,
                VehicleId = vehicle.Id,
                VehiclePlate = vehicle.RegistrationPlate,
                OccurredAt = occurredAt,
                PosOccurredAtLocal = TimeZoneInfo.ConvertTime(occurredAt, PointOfSaleTimeZone),
                PosTimeZoneId = PointOfSaleTimeZoneId,
                StationName = StationNames[random.Next(0, StationNames.Length)],
                FuelType = vehicle.FuelType,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Discount = discount,
                NetTotal = netTotal,
                VatRate = vatRate,
                VatAmount = vatAmount,
                GrossTotal = netTotal + vatAmount,
                Status = TransactionStatus.Completed,
            };
        }

        return transactions;
    }

    private static StableRandom CreateRandom(
        Guid cardId,
        Guid vehicleId,
        DateTimeOffset issuedAtUtc
    )
    {
        var seed = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{cardId:N}:{vehicleId:N}:{issuedAtUtc.UtcDateTime.Ticks}")
        );
        return new StableRandom(BinaryPrimitives.ReadUInt32LittleEndian(seed));
    }

    private static Guid CreateTransactionId(
        Guid cardId,
        Guid vehicleId,
        DateTimeOffset issuedAtUtc,
        int index
    )
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                $"transaction:{cardId:N}:{vehicleId:N}:{issuedAtUtc.UtcDateTime.Ticks}:{index}"
            )
        );
        return new Guid(hash.AsSpan(0, 16));
    }

    private struct StableRandom(uint seed)
    {
        private uint state = seed == 0 ? 0x9E3779B9u : seed;

        public int Next(int minimum, int exclusiveMaximum)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return minimum + (int)(state % (uint)(exclusiveMaximum - minimum));
        }
    }
}
