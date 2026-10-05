using System.Data;
using FleetCards.Application;
using FleetCards.Contracts;
using FleetCards.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetCards.Infrastructure;

public sealed class EfProgramAssignmentService(FleetCardsDbContext dbContext)
    : IProgramAssignmentService
{
    public async Task<IReadOnlyList<FleetCardProgramAssignmentResponse>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var clientId = await GetClientIdAsync(cancellationToken);
        return await GetAssignmentsAsync(clientId, cancellationToken);
    }

    public async Task<ProgramAssignmentCommandResult> ScheduleSwitchAsync(
        Guid programId,
        DateTimeOffset startsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var clientId = await GetClientIdAsync(cancellationToken);
        if (!await dbContext.Programs.AnyAsync(program => program.Id == programId, cancellationToken))
        {
            return ProgramAssignmentCommandResult.NotFound();
        }

        var assignments = await dbContext
            .ClientProgramAssignments.Where(assignment => assignment.ClientId == clientId)
            .ToArrayAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var plan = ProgramAssignmentService.PlanSwitch(
            assignments,
            clientId,
            programId,
            startsAt,
            now
        );
        if (!plan.IsValid)
        {
            return ProgramAssignmentCommandResult.Invalid(plan.ValidationErrors!);
        }

        ApplyChanges(assignments, plan);
        var newAssignment = plan.NewAssignment!;
        dbContext.ClientProgramAssignments.Add(newAssignment);
        dbContext.AuditEvents.Add(
            CreateAudit(
                newAssignment.Id,
                "ProgramAssignmentScheduled",
                actor,
                correlationId,
                now
            )
        );
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProgramAssignmentCommandResult.Success();
    }

    public async Task<ProgramAssignmentCommandResult> EndAsync(
        DateTimeOffset endsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var clientId = await GetClientIdAsync(cancellationToken);
        var assignments = await dbContext
            .ClientProgramAssignments.Where(assignment => assignment.ClientId == clientId)
            .ToArrayAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var plan = ProgramAssignmentService.PlanEnd(assignments, clientId, endsAt, now);
        if (!plan.IsValid)
        {
            return ProgramAssignmentCommandResult.Invalid(plan.ValidationErrors!);
        }

        ApplyChanges(assignments, plan);
        dbContext.AuditEvents.Add(
            CreateAudit(
                plan.ActiveAssignmentId!.Value,
                "ProgramAssignmentEnded",
                actor,
                correlationId,
                now
            )
        );
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProgramAssignmentCommandResult.Success();
    }

    private async Task<Guid> GetClientIdAsync(CancellationToken cancellationToken) =>
        await dbContext.Clients.Select(client => client.Id).SingleAsync(cancellationToken);

    private async Task<IReadOnlyList<FleetCardProgramAssignmentResponse>> GetAssignmentsAsync(
        Guid clientId,
        CancellationToken cancellationToken
    ) =>
        await dbContext
            .ClientProgramAssignments.AsNoTracking()
            .Where(assignment => assignment.ClientId == clientId)
            .OrderBy(assignment => assignment.StartsAt)
            .Select(assignment => new FleetCardProgramAssignmentResponse(
                assignment.Id,
                assignment.ProgramId,
                assignment.StartsAt,
                assignment.EndsAt
            ))
            .ToArrayAsync(cancellationToken);

    private void ApplyChanges(
        IReadOnlyList<ClientProgramAssignment> assignments,
        ProgramAssignmentPlan plan
    )
    {
        var replacedIds = plan.PendingAssignmentIdsToReplace.ToHashSet();
        dbContext.ClientProgramAssignments.RemoveRange(
            assignments.Where(assignment => replacedIds.Contains(assignment.Id))
        );
        if (plan.ActiveAssignmentId is not null)
        {
            assignments.Single(assignment => assignment.Id == plan.ActiveAssignmentId.Value).EndsAt =
                plan.ActiveEndsAt;
        }
    }

    private static FleetAuditEvent CreateAudit(
        Guid assignmentId,
        string action,
        string actor,
        string correlationId,
        DateTimeOffset occurredAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Actor = actor,
            Action = action,
            EntityType = "ClientProgramAssignment",
            EntityId = assignmentId,
            OccurredAt = occurredAt,
            Succeeded = true,
            CorrelationId = correlationId,
        };
}
