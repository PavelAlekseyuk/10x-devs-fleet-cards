using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FleetCards.Contracts;
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
    private readonly IProgramResolutionService? programResolutionService;

    public MockFuelTransactionGenerator(
        decimal vatRate = DefaultVatRate,
        IProgramResolutionService? programResolutionService = null
    )
    {
        if (vatRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        }

        this.vatRate = vatRate;
        this.programResolutionService = programResolutionService;
    }

    public IReadOnlyList<FuelTransaction> Generate(
        Guid cardId,
        FleetCar vehicle,
        DateTimeOffset issuedAt,
        TransactionReportPeriod? period = null
    )
    {
        var issuedAtUtc = issuedAt.ToUniversalTime();
        var (periodStart, periodEnd) = period is null
            ? GetPreviousPointOfSaleMonth(issuedAtUtc)
            : (period.StartUtc.ToUniversalTime(), period.EndUtc.ToUniversalTime());
        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("The transaction period must have a positive duration.");
        }

        var random = CreateRandom(cardId, vehicle.Id, issuedAtUtc);
        var count = random.Next(5, 16);
        var transactions = new FuelTransaction[count];

        for (var index = 0; index < count; index++)
        {
            var quantity = random.Next(15_000, 55_001) / 1_000m;
            var unitPrice = random.Next(450, 801) / 100m;
            var occurredAt = period is null
                ? periodStart.AddMinutes(
                    random.Next(0, (int)(periodEnd - periodStart).TotalMinutes)
                )
                : periodStart.AddTicks(random.NextInt64((periodEnd - periodStart).Ticks));
            var percent =
                programResolutionService?.GetPercent(vehicle.ClientId, vehicle.FuelType, occurredAt)
                ?? 0m;
            var discount = decimal.Round(
                unitPrice * percent,
                4,
                MidpointRounding.AwayFromZero
            );
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

    private static (DateTimeOffset Start, DateTimeOffset End) GetPreviousPointOfSaleMonth(
        DateTimeOffset issuedAtUtc
    )
    {
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

        return (
            new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(previousLocalMonthStart, PointOfSaleTimeZone)
            ),
            new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(currentLocalMonthStart, PointOfSaleTimeZone)
            )
        );
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

        public long NextInt64(long exclusiveMaximum)
        {
            var high = (uint)Next(0, int.MaxValue);
            var low = (uint)Next(0, int.MaxValue);
            var value = ((ulong)high << 32) | low;
            return (long)(value % (ulong)exclusiveMaximum);
        }
    }
}
