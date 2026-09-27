using FleetCards.Contracts;
using FleetCards.Domain;

namespace FleetCards.Application;

public interface ITransactionReportingService
{
    TransactionReportPeriod ResolvePeriod(TransactionReportRequest request);

    TransactionReportResponse CreateReport(
        TransactionReportPeriod period,
        IEnumerable<FuelTransaction> transactions
    );

    DashboardSummaryResponse CreateSummary(
        TransactionReportPeriod period,
        IEnumerable<FuelTransaction> transactions,
        int activeVehicleCount,
        int activeCardCount
    );
}

public sealed class TransactionReportingService : ITransactionReportingService
{
    public const string DefaultTimeZoneId = "Europe/Warsaw";
    private readonly string defaultTimeZoneId;

    public TransactionReportingService(string defaultTimeZoneId = DefaultTimeZoneId)
    {
        ResolveTimeZone(defaultTimeZoneId);
        this.defaultTimeZoneId = defaultTimeZoneId;
    }

    public TransactionReportPeriod ResolvePeriod(TransactionReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Year is < 1 or > 9998)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Year must be between 1 and 9998."
            );
        }

        if (request.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Month must be between 1 and 12."
            );
        }

        var timeZoneId = request.TimeZoneId ?? defaultTimeZoneId;
        var timeZone = ResolveTimeZone(timeZoneId);
        var localStart = new DateTime(
            request.Year,
            request.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified
        );
        var nextMonth = localStart.AddMonths(1);

        return new TransactionReportPeriod(
            request.Year,
            request.Month,
            timeZoneId,
            ToUtcBoundary(localStart, timeZone),
            ToUtcBoundary(nextMonth, timeZone)
        );
    }

    public TransactionReportResponse CreateReport(
        TransactionReportPeriod period,
        IEnumerable<FuelTransaction> transactions
    )
    {
        ValidatePeriod(period);
        ArgumentNullException.ThrowIfNull(transactions);

        var timeZone = ResolveTimeZone(period.TimeZoneId);
        var facts = transactions.ToArray();
        ValidateLineages(facts);

        var effectivePurchases = facts
            .Where(IsPurchaseVersion)
            .GroupBy(transaction => transaction.LineageId)
            .Select(lineage =>
                lineage
                    .OrderByDescending(transaction => transaction.Version)
                    .ThenByDescending(transaction => transaction.Id)
                    .First()
            );
        var correctionEvents = facts.Where(IsCorrectionEvent);
        var rows = effectivePurchases
            .Concat(correctionEvents)
            .Where(transaction =>
            {
                var occurredAtUtc = transaction.OccurredAt.ToUniversalTime();
                return occurredAtUtc >= period.StartUtc && occurredAtUtc < period.EndUtc;
            })
            .Select(transaction => ToResponse(transaction, timeZone))
            .OrderBy(transaction => transaction.OccurredAtUtc)
            .ThenBy(transaction => transaction.Id)
            .ToArray();

        return new TransactionReportResponse(
            period.Year,
            period.Month,
            period.TimeZoneId,
            period.StartUtc,
            period.EndUtc,
            rows,
            CalculateTotals(rows)
        );
    }

    public DashboardSummaryResponse CreateSummary(
        TransactionReportPeriod period,
        IEnumerable<FuelTransaction> transactions,
        int activeVehicleCount,
        int activeCardCount
    )
    {
        if (activeVehicleCount < 0 || activeCardCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeVehicleCount),
                "Active entity counts cannot be negative."
            );
        }

        var report = CreateReport(period, transactions);
        return new DashboardSummaryResponse(
            activeVehicleCount,
            activeCardCount,
            report.Totals,
            report.PeriodStartUtc,
            report.PeriodEndUtc
        );
    }

    public static decimal CalculateNetTotal(
        decimal quantity,
        decimal unitPrice,
        decimal discount
    ) => decimal.Round(quantity * (unitPrice - discount), 2, MidpointRounding.AwayFromZero);

    public static decimal CalculateVatAmount(decimal netTotal, decimal vatRate) =>
        decimal.Round(netTotal * vatRate, 2, MidpointRounding.AwayFromZero);

    private static TransactionReportRowResponse ToResponse(
        FuelTransaction transaction,
        TimeZoneInfo userTimeZone
    )
    {
        var occurredAtUtc = transaction.OccurredAt.ToUniversalTime();
        return new TransactionReportRowResponse(
            transaction.Id,
            transaction.LineageId,
            transaction.Version,
            transaction.CorrectsTransactionId,
            transaction.CardId,
            transaction.VehicleId,
            transaction.VehiclePlate,
            occurredAtUtc,
            TimeZoneInfo.ConvertTime(occurredAtUtc, userTimeZone),
            transaction.PosOccurredAtLocal,
            transaction.PosTimeZoneId,
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
        );
    }

    private static TransactionReportTotals CalculateTotals(
        IReadOnlyCollection<TransactionReportRowResponse> rows
    )
    {
        var grossSavingsTotal = rows.Sum(row =>
        {
            var posNet = decimal.Round(
                row.Quantity * row.UnitPrice,
                2,
                MidpointRounding.AwayFromZero
            );
            var posVat = decimal.Round(posNet * row.VatRate, 2, MidpointRounding.AwayFromZero);
            var posGross = posNet + posVat;
            return posGross - row.GrossTotal;
        });

        return new TransactionReportTotals(
            decimal.Round(rows.Sum(row => row.NetTotal), 2, MidpointRounding.AwayFromZero),
            decimal.Round(rows.Sum(row => row.VatAmount), 2, MidpointRounding.AwayFromZero),
            decimal.Round(rows.Sum(row => row.GrossTotal), 2, MidpointRounding.AwayFromZero),
            grossSavingsTotal,
            rows.Count
        );
    }

    private static bool IsPurchaseVersion(FuelTransaction transaction) =>
        transaction.Status is TransactionStatus.Completed or TransactionStatus.Recalculated;

    private static bool IsCorrectionEvent(FuelTransaction transaction) =>
        transaction.Status is TransactionStatus.Refunded or TransactionStatus.Cancelled;

    private static void ValidateLineages(IReadOnlyCollection<FuelTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            var valid = transaction.Status switch
            {
                TransactionStatus.Completed => transaction.Id == transaction.LineageId
                    && transaction.Version == 1
                    && transaction.CorrectsTransactionId is null,
                TransactionStatus.Recalculated => transaction.Id != transaction.LineageId
                    && transaction.Version is > 1
                    && transaction.CorrectsTransactionId == transaction.LineageId,
                TransactionStatus.Refunded or TransactionStatus.Cancelled => transaction.Id
                    != transaction.LineageId
                    && transaction.Version is null
                    && transaction.CorrectsTransactionId == transaction.LineageId,
                _ => false,
            };

            if (!valid)
            {
                throw new ArgumentException(
                    $"Transaction {transaction.Id} has invalid lineage or version metadata.",
                    nameof(transactions)
                );
            }
        }

        var purchaseLineages = transactions
            .Where(transaction => transaction.Status == TransactionStatus.Completed)
            .Select(transaction => transaction.LineageId)
            .ToHashSet();

        if (
            transactions.Any(transaction =>
                transaction.Status != TransactionStatus.Completed
                && !purchaseLineages.Contains(transaction.LineageId)
            )
        )
        {
            throw new ArgumentException(
                "Every recalculation or correction must reference an existing purchase lineage.",
                nameof(transactions)
            );
        }

        if (
            transactions
                .Where(IsPurchaseVersion)
                .GroupBy(transaction => (transaction.LineageId, transaction.Version))
                .Any(version => version.Count() > 1)
        )
        {
            throw new ArgumentException(
                "Purchase version numbers must be unique within a lineage.",
                nameof(transactions)
            );
        }

        var purchaseTimes = transactions
            .Where(IsPurchaseVersion)
            .GroupBy(transaction => transaction.LineageId);
        if (
            purchaseTimes.Any(lineage =>
                lineage
                    .Select(transaction => transaction.OccurredAt.ToUniversalTime())
                    .Distinct()
                    .Count() > 1
            )
        )
        {
            throw new ArgumentException(
                "Purchase recalculations must retain the original occurrence time.",
                nameof(transactions)
            );
        }
    }

    private static void ValidatePeriod(TransactionReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        var expected = new TransactionReportingService().ResolvePeriod(
            new TransactionReportRequest(period.Year, period.Month, period.TimeZoneId)
        );
        if (expected != period)
        {
            throw new ArgumentException("The reporting period is invalid.", nameof(period));
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (
            string.IsNullOrWhiteSpace(timeZoneId)
            || !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _)
        )
        {
            throw new ArgumentException(
                "A supported IANA timezone ID is required.",
                nameof(timeZoneId)
            );
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException(
                "The timezone ID is not available on this system.",
                nameof(timeZoneId),
                exception
            );
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException(
                "The timezone data is invalid on this system.",
                nameof(timeZoneId),
                exception
            );
        }
    }

    private static DateTimeOffset ToUtcBoundary(DateTime localBoundary, TimeZoneInfo timeZone)
    {
        while (timeZone.IsInvalidTime(localBoundary))
        {
            localBoundary = localBoundary.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(localBoundary))
        {
            var earliestOffset = timeZone.GetAmbiguousTimeOffsets(localBoundary).Max();
            return new DateTimeOffset(localBoundary, earliestOffset).ToUniversalTime();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localBoundary, timeZone));
    }
}
