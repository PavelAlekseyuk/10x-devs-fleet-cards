using FleetCards.Domain;

namespace FleetCards.Application;

public sealed record ProgramAssignmentPlan(
    IReadOnlyList<Guid> PendingAssignmentIdsToReplace,
    Guid? ActiveAssignmentId,
    DateTimeOffset? ActiveEndsAt,
    ClientProgramAssignment? NewAssignment,
    IReadOnlyDictionary<string, string[]>? ValidationErrors
)
{
    public bool IsValid => ValidationErrors is null;
}

public static class ProgramAssignmentService
{
    public static ProgramAssignmentPlan PlanSwitch(
        IReadOnlyList<ClientProgramAssignment> assignments,
        Guid clientId,
        Guid programId,
        DateTimeOffset startsAt,
        DateTimeOffset now
    )
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        startsAt = startsAt.ToUniversalTime();
        now = now.ToUniversalTime();
        if (programId == Guid.Empty)
        {
            errors[nameof(programId)] = ["A valid program is required."];
        }

        if (startsAt <= now)
        {
            errors[nameof(startsAt)] = ["The start time must be in the future."];
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var clientAssignments = assignments.Where(assignment => assignment.ClientId == clientId);
        var pending = clientAssignments.Where(assignment => assignment.StartsAt > now).ToArray();
        var activeAssignments = clientAssignments
            .Where(assignment =>
                assignment.StartsAt <= now && (assignment.EndsAt is null || assignment.EndsAt > now)
            )
            .ToArray();
        if (activeAssignments.Length > 1)
        {
            return Invalid(OverlapErrors());
        }

        var active = activeAssignments.SingleOrDefault();
        var proposed = clientAssignments
            .Where(assignment =>
                assignment.Id != active?.Id && !pending.Any(item => item.Id == assignment.Id)
            )
            .ToList();
        if (active is not null)
        {
            proposed.Add(
                new ClientProgramAssignment
                {
                    Id = active.Id,
                    ClientId = active.ClientId,
                    ProgramId = active.ProgramId,
                    StartsAt = active.StartsAt,
                    EndsAt = startsAt,
                }
            );
        }

        var newAssignment = new ClientProgramAssignment
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ProgramId = programId,
            StartsAt = startsAt,
        };
        proposed.Add(newAssignment);

        if (HasOverlap(proposed))
        {
            return Invalid(OverlapErrors());
        }

        return new ProgramAssignmentPlan(
            pending.Select(assignment => assignment.Id).ToArray(),
            active?.Id,
            active is null ? null : startsAt,
            newAssignment,
            null
        );
    }

    public static ProgramAssignmentPlan PlanEnd(
        IReadOnlyList<ClientProgramAssignment> assignments,
        Guid clientId,
        DateTimeOffset endsAt,
        DateTimeOffset now
    )
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        endsAt = endsAt.ToUniversalTime();
        now = now.ToUniversalTime();
        if (endsAt <= now)
        {
            errors[nameof(endsAt)] = ["The end time must be in the future."];
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var clientAssignments = assignments.Where(assignment => assignment.ClientId == clientId);
        var pending = clientAssignments.Where(assignment => assignment.StartsAt > now).ToArray();
        var activeAssignments = clientAssignments
            .Where(assignment =>
                assignment.StartsAt <= now && (assignment.EndsAt is null || assignment.EndsAt > now)
            )
            .ToArray();
        if (activeAssignments.Length > 1)
        {
            return Invalid(OverlapErrors());
        }

        var active = activeAssignments.SingleOrDefault();
        if (active is null)
        {
            return Invalid(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(endsAt)] = ["There is no active assignment to end."],
                }
            );
        }

        var proposed = clientAssignments
            .Where(assignment =>
                assignment.Id != active.Id && !pending.Any(item => item.Id == assignment.Id)
            )
            .Append(
                new ClientProgramAssignment
                {
                    Id = active.Id,
                    ClientId = active.ClientId,
                    ProgramId = active.ProgramId,
                    StartsAt = active.StartsAt,
                    EndsAt = endsAt,
                }
            )
            .ToArray();
        if (HasOverlap(proposed))
        {
            return Invalid(OverlapErrors());
        }

        return new ProgramAssignmentPlan(
            pending.Select(assignment => assignment.Id).ToArray(),
            active.Id,
            endsAt,
            null,
            null
        );
    }

    private static bool HasOverlap(IReadOnlyList<ClientProgramAssignment> assignments)
    {
        var ordered = assignments.OrderBy(assignment => assignment.StartsAt).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            if (previous.EndsAt is null || previous.EndsAt > ordered[index].StartsAt)
            {
                return true;
            }
        }

        return false;
    }

    private static ProgramAssignmentPlan Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new([], null, null, null, errors);

    private static IReadOnlyDictionary<string, string[]> OverlapErrors() =>
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["assignments"] = ["The resulting program assignments must not overlap."],
        };
}

public enum ProgramAssignmentCommandError
{
    None,
    Validation,
    NotFound,
}

public sealed record ProgramAssignmentCommandResult(
    ProgramAssignmentCommandError Error,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
)
{
    public static ProgramAssignmentCommandResult Success() =>
        new(ProgramAssignmentCommandError.None);

    public static ProgramAssignmentCommandResult Invalid(
        IReadOnlyDictionary<string, string[]> errors
    ) => new(ProgramAssignmentCommandError.Validation, errors);

    public static ProgramAssignmentCommandResult NotFound() =>
        new(ProgramAssignmentCommandError.NotFound);
}

public interface IProgramAssignmentService
{
    Task<IReadOnlyList<FleetCards.Contracts.FleetCardProgramAssignmentResponse>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default
    );

    Task<ProgramAssignmentCommandResult> ScheduleSwitchAsync(
        Guid programId,
        DateTimeOffset startsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );

    Task<ProgramAssignmentCommandResult> EndAsync(
        DateTimeOffset endsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    );
}
