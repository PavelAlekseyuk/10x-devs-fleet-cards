using FleetCards.Contracts;

namespace FleetCards.Application;

public sealed class ProgramComparisonService(
    IFleetCardsReadService readService,
    ITransactionReportingService reportingService,
    IProgramResolutionDataSource programDataSource
)
{
    public async Task<ProgramComparisonResponse> GetComparisonAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default
    )
    {
        if (from >= to)
        {
            throw new ArgumentException("The start of the range must be before its end.");
        }

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();
        if (fromUtc.Year > 9998 || toUtc.Year > 9998)
        {
            throw new ArgumentOutOfRangeException(
                nameof(to),
                "The range must end within a supported reporting year."
            );
        }

        var eligibleTransactions = new List<TransactionReportRowResponse>();
        var month = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (month < toUtc.UtcDateTime)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var period = reportingService.ResolvePeriod(
                new TransactionReportRequest(month.Year, month.Month, "UTC")
            );
            var report = await readService.GetTransactionReportAsync(period, cancellationToken);
            eligibleTransactions.AddRange(
                report.Transactions.Where(transaction =>
                    transaction.OccurredAtUtc >= fromUtc && transaction.OccurredAtUtc < toUtc
                )
            );
            month = month.AddMonths(1);
        }

        var baseline = new ProgramComparisonBaseline(
            RoundTotal(eligibleTransactions.Sum(transaction => transaction.NetTotal)),
            RoundTotal(eligibleTransactions.Sum(transaction => transaction.VatAmount)),
            RoundTotal(eligibleTransactions.Sum(transaction => transaction.GrossTotal))
        );
        var rules = programDataSource
            .GetRules()
            .ToDictionary(rule => (rule.ProgramId, rule.FuelType), rule => rule.Percent);
        var comparisons = readService
            .GetPrograms()
            .Select(program =>
            {
                decimal netTotal = 0m;
                decimal grossTotal = 0m;
                foreach (var transaction in eligibleTransactions)
                {
                    var percent = rules.GetValueOrDefault((program.Id, transaction.FuelType));
                    var discount = decimal.Round(
                        transaction.UnitPrice * percent,
                        4,
                        MidpointRounding.AwayFromZero
                    );
                    var net = TransactionReportingService.CalculateNetTotal(
                        transaction.Quantity,
                        transaction.UnitPrice,
                        discount
                    );
                    var vat = TransactionReportingService.CalculateVatAmount(
                        net,
                        transaction.VatRate
                    );
                    netTotal += net;
                    grossTotal += net + vat;
                }

                netTotal = RoundTotal(netTotal);
                grossTotal = RoundTotal(grossTotal);
                return new ProgramComparisonProgramResponse(
                    program.Id,
                    program.Name,
                    netTotal,
                    grossTotal,
                    baseline.NetTotal - netTotal,
                    baseline.GrossTotal - grossTotal
                );
            })
            .OrderBy(program => program.HypotheticalGrossTotal)
            .ThenBy(program => program.ProgramName, StringComparer.Ordinal)
            .ToArray();

        return new ProgramComparisonResponse(fromUtc, toUtc, baseline, comparisons);
    }

    private static decimal RoundTotal(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
