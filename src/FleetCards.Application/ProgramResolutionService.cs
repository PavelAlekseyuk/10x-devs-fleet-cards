using FleetCards.Domain;

namespace FleetCards.Application;

public interface IProgramResolutionService
{
    decimal GetPercent(Guid clientId, string fuelType, DateTimeOffset occurredAt);
}

public interface IProgramResolutionDataSource
{
    IReadOnlyList<FleetCardProgramRule> GetRules();
    IReadOnlyList<ClientProgramAssignment> GetAssignments();
}

public sealed class ProgramResolutionService : IProgramResolutionService
{
    private readonly Lazy<IReadOnlyList<FleetCardProgramRule>> rules;
    private readonly Lazy<IReadOnlyList<ClientProgramAssignment>> assignments;

    public ProgramResolutionService(IProgramResolutionDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        rules = new Lazy<IReadOnlyList<FleetCardProgramRule>>(dataSource.GetRules);
        assignments = new Lazy<IReadOnlyList<ClientProgramAssignment>>(
            dataSource.GetAssignments
        );
    }

    public decimal GetPercent(Guid clientId, string fuelType, DateTimeOffset occurredAt)
    {
        if (clientId == Guid.Empty)
        {
            return 0m;
        }

        var assignment = assignments
            .Value.Where(candidate =>
                candidate.ClientId == clientId
                && occurredAt >= candidate.StartsAt
                && (candidate.EndsAt is null || occurredAt < candidate.EndsAt.Value)
            )
            .OrderByDescending(candidate => candidate.StartsAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            return 0m;
        }

        return rules
            .Value.Where(rule =>
                rule.ProgramId == assignment.ProgramId
                && string.Equals(rule.FuelType, fuelType, StringComparison.Ordinal)
            )
            .Select(rule => (decimal?)rule.Percent)
            .FirstOrDefault()
            ?? 0m;
    }
}
