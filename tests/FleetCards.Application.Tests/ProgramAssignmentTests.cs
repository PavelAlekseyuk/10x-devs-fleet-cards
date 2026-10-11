using FleetCards.Application;
using FleetCards.Domain;

namespace FleetCards.Application.Tests;

public sealed class ProgramAssignmentTests
{
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid CurrentProgramId = Guid.NewGuid();
    private static readonly Guid PendingProgramId = Guid.NewGuid();
    private static readonly Guid SelectedProgramId = Guid.NewGuid();

    [Fact]
    public void PlanSwitch_replaces_pending_assignment_and_ends_active_at_switch_instant()
    {
        var now = DateTimeOffset.UtcNow;
        var active = Assignment(CurrentProgramId, now.AddDays(-1), now.AddDays(1));
        var pending = Assignment(PendingProgramId, now.AddDays(1), null);
        var switchAt = now.AddDays(2);

        var plan = ProgramAssignmentService.PlanSwitch(
            [active, pending],
            ClientId,
            SelectedProgramId,
            switchAt,
            now
        );

        Assert.True(plan.IsValid);
        Assert.Equal(active.Id, plan.ActiveAssignmentId);
        Assert.Equal(switchAt, plan.ActiveEndsAt);
        Assert.Equal(new[] { pending.Id }, plan.PendingAssignmentIdsToReplace);
        Assert.NotNull(plan.NewAssignment);
        Assert.Equal(SelectedProgramId, plan.NewAssignment.ProgramId);
        Assert.Equal(switchAt, plan.NewAssignment.StartsAt);
        Assert.Null(plan.NewAssignment.EndsAt);
    }

    [Fact]
    public void PlanSwitch_allows_adjacent_half_open_intervals_but_rejects_a_past_start()
    {
        var now = DateTimeOffset.UtcNow;
        var prior = Assignment(CurrentProgramId, now.AddDays(-3), now.AddDays(-1));
        var active = Assignment(PendingProgramId, now.AddDays(-1), null);

        var validPlan = ProgramAssignmentService.PlanSwitch(
            [prior, active],
            ClientId,
            SelectedProgramId,
            now.AddDays(1),
            now
        );
        var invalidPlan = ProgramAssignmentService.PlanSwitch(
            [prior, active],
            ClientId,
            SelectedProgramId,
            now,
            now
        );

        Assert.True(validPlan.IsValid);
        Assert.False(invalidPlan.IsValid);
        Assert.Contains("startsAt", invalidPlan.ValidationErrors!.Keys);
    }

    [Fact]
    public void PlanSwitch_rejects_existing_overlapping_intervals()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Assignment(CurrentProgramId, now.AddDays(-2), now.AddDays(2));
        var second = Assignment(PendingProgramId, now.AddDays(-1), now.AddDays(3));

        var plan = ProgramAssignmentService.PlanSwitch(
            [first, second],
            ClientId,
            SelectedProgramId,
            now.AddDays(1),
            now
        );

        Assert.False(plan.IsValid);
        Assert.Contains("assignments", plan.ValidationErrors!.Keys);
    }

    [Fact]
    public void PlanEnd_replaces_pending_assignment_and_leaves_no_successor()
    {
        var now = DateTimeOffset.UtcNow;
        var active = Assignment(CurrentProgramId, now.AddDays(-1), now.AddDays(1));
        var pending = Assignment(PendingProgramId, now.AddDays(1), null);
        var endAt = now.AddDays(2);

        var plan = ProgramAssignmentService.PlanEnd([active, pending], ClientId, endAt, now);

        Assert.True(plan.IsValid);
        Assert.Equal(active.Id, plan.ActiveAssignmentId);
        Assert.Equal(endAt, plan.ActiveEndsAt);
        Assert.Equal(new[] { pending.Id }, plan.PendingAssignmentIdsToReplace);
        Assert.Null(plan.NewAssignment);
    }

    [Fact]
    public void PlanEnd_requires_an_active_assignment_and_a_future_end()
    {
        var now = DateTimeOffset.UtcNow;
        var gapPlan = ProgramAssignmentService.PlanEnd([], ClientId, now.AddDays(1), now);
        var pastPlan = ProgramAssignmentService.PlanEnd(
            [Assignment(CurrentProgramId, now.AddDays(-1), null)],
            ClientId,
            now,
            now
        );

        Assert.False(gapPlan.IsValid);
        Assert.Contains("endsAt", gapPlan.ValidationErrors!.Keys);
        Assert.False(pastPlan.IsValid);
        Assert.Contains("endsAt", pastPlan.ValidationErrors!.Keys);
    }

    private static ClientProgramAssignment Assignment(
        Guid programId,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            ProgramId = programId,
            StartsAt = startsAt,
            EndsAt = endsAt,
        };
}
