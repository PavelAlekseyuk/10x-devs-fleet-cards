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
    IReadOnlyList<FleetCardResponse> GetCards();
    Task<TransactionReportResponse> GetTransactionReportAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    );
    Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    );
    IReadOnlyList<FleetCardProgramResponse> GetPrograms();
}

public sealed class SeededProgramResolutionDataSource : IProgramResolutionDataSource
{
    private static readonly Guid NebulaBasicId = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );
    private static readonly Guid OrionDieselId = Guid.Parse(
        "55555555-5555-5555-5555-555555555556"
    );
    private static readonly Guid AndromedaPetrolId = Guid.Parse(
        "55555555-5555-5555-5555-555555555557"
    );
    private static readonly Guid VoyagerBalancedId = Guid.Parse(
        "55555555-5555-5555-5555-555555555558"
    );
    private static readonly FleetCardProgramRule[] Rules =
    [
        .. CreateRules(NebulaBasicId, 0.10m, -0.02m),
        .. CreateRules(OrionDieselId, 0m, 0m),
        .. CreateRules(AndromedaPetrolId, -0.03m, 0.09m),
        .. CreateRules(VoyagerBalancedId, 0.03m, 0.02m),
    ];
    internal static readonly object AssignmentLock = new();
    private static readonly List<ClientProgramAssignment> Assignments =
    [
        new()
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProgramId = NebulaBasicId,
            StartsAt = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        },
    ];

    public IReadOnlyList<FleetCardProgramRule> GetRules() => Rules;

    public IReadOnlyList<ClientProgramAssignment> GetAssignments() => Assignments;

    internal static IReadOnlyList<ClientProgramAssignment> GetAssignmentSnapshot()
    {
        lock (AssignmentLock)
        {
            return Assignments.ToArray();
        }
    }

    internal static void ApplyPlan(ProgramAssignmentPlan plan)
    {
        lock (AssignmentLock)
        {
            Assignments.RemoveAll(assignment =>
                plan.PendingAssignmentIdsToReplace.Contains(assignment.Id)
            );
            if (plan.ActiveAssignmentId is not null)
            {
                Assignments
                    .Single(assignment => assignment.Id == plan.ActiveAssignmentId.Value)
                    .EndsAt = plan.ActiveEndsAt;
            }

            if (plan.NewAssignment is not null)
            {
                Assignments.Add(plan.NewAssignment);
            }
        }
    }

    private static FleetCardProgramRule[] CreateRules(
        Guid programId,
        decimal dieselPercent,
        decimal petrolPercent
    ) =>
        [
            new() { ProgramId = programId, FuelType = "Diesel", Percent = dieselPercent },
            new() { ProgramId = programId, FuelType = "Diesel+", Percent = dieselPercent },
            new() { ProgramId = programId, FuelType = "95", Percent = petrolPercent },
            new() { ProgramId = programId, FuelType = "98", Percent = petrolPercent },
            new() { ProgramId = programId, FuelType = "95+", Percent = petrolPercent },
            new() { ProgramId = programId, FuelType = "98+", Percent = petrolPercent },
        ];
}

public sealed class SeededFleetCardsReadService(
    IFleetCardService cardService,
    IFleetVehicleService vehicleService,
    ITransactionReportingService reportingService,
    IProgramResolutionDataSource programDataSource
) : IFleetCardsReadService
{
    public CurrentUserResponse GetCurrentUser() => new("anna.nowak", "ReadWrite");

    public ClientResponse GetClient() =>
        new(
            "Baltic Logistics Sp. z o.o.",
            "5250001234",
            "Poland",
            "ul. Prosta 12, 00-850 Warszawa"
        );

    public IReadOnlyList<FleetCardResponse> GetCards() => cardService.GetCards();

    public Task<TransactionReportResponse> GetTransactionReportAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            reportingService.CreateReport(period, cardService.GetTransactionFacts())
        );
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        TransactionReportPeriod period,
        CancellationToken cancellationToken = default
    )
    {
        var activeVehicles = await vehicleService.GetCarsAsync(
            search: null,
            includeArchived: false,
            cancellationToken
        );
        var activeCards = cardService
            .GetCards()
            .Count(card => card.State == CardState.Active.ToString());
        return reportingService.CreateSummary(
            period,
            cardService.GetTransactionFacts(),
            activeVehicles.Count,
            activeCards
        );
    }

    public IReadOnlyList<FleetCardProgramResponse> GetPrograms()
    {
        var programs = new[]
        {
            (Guid.Parse("55555555-5555-5555-5555-555555555557"), "Andromeda Petrol"),
            (Guid.Parse("55555555-5555-5555-5555-555555555555"), "Nebula Basic"),
            (Guid.Parse("55555555-5555-5555-5555-555555555556"), "Orion Diesel"),
            (Guid.Parse("55555555-5555-5555-5555-555555555558"), "Voyager Balanced"),
        };
        var rules = programDataSource.GetRules();
        return programs
            .Select(program => new FleetCardProgramResponse(
                program.Item1,
                program.Item2,
                new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
                null,
                rules.Where(rule => rule.ProgramId == program.Item1)
                    .Select(rule => new FleetCardProgramRuleResponse(rule.FuelType, rule.Percent))
                    .OrderBy(rule => rule.FuelType, StringComparer.Ordinal)
                    .ToArray()
            ))
            .ToArray();
    }
}

public sealed class SeededProgramAssignmentService : IProgramAssignmentService
{
    private static readonly Guid ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IProgramResolutionDataSource dataSource;
    private readonly List<FleetAuditEvent> auditEvents = [];

    public SeededProgramAssignmentService(IProgramResolutionDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public Task<IReadOnlyList<FleetCardProgramAssignmentResponse>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (SeededProgramResolutionDataSource.AssignmentLock)
        {
            return Task.FromResult<IReadOnlyList<FleetCardProgramAssignmentResponse>>(
                SeededProgramResolutionDataSource
                    .GetAssignmentSnapshot()
                    .OrderBy(assignment => assignment.StartsAt)
                    .Select(ToResponse)
                    .ToArray()
            );
        }
    }

    public IReadOnlyList<FleetAuditEvent> GetAuditEvents()
    {
        lock (SeededProgramResolutionDataSource.AssignmentLock)
        {
            return auditEvents.ToArray();
        }
    }

    public Task<ProgramAssignmentCommandResult> ScheduleSwitchAsync(
        Guid programId,
        DateTimeOffset startsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!dataSource.GetRules().Any(rule => rule.ProgramId == programId))
        {
            return Task.FromResult(ProgramAssignmentCommandResult.NotFound());
        }

        lock (SeededProgramResolutionDataSource.AssignmentLock)
        {
            var now = DateTimeOffset.UtcNow;
            var plan = ProgramAssignmentService.PlanSwitch(
                SeededProgramResolutionDataSource.GetAssignmentSnapshot(),
                ClientId,
                programId,
                startsAt,
                now
            );
            if (!plan.IsValid)
            {
                return Task.FromResult(
                    ProgramAssignmentCommandResult.Invalid(plan.ValidationErrors!)
                );
            }

            SeededProgramResolutionDataSource.ApplyPlan(plan);
            RecordAudit(
                plan.NewAssignment!.Id,
                "ProgramAssignmentScheduled",
                actor,
                correlationId,
                now
            );
            return Task.FromResult(ProgramAssignmentCommandResult.Success());
        }
    }

    public Task<ProgramAssignmentCommandResult> EndAsync(
        DateTimeOffset endsAt,
        string actor,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (SeededProgramResolutionDataSource.AssignmentLock)
        {
            var now = DateTimeOffset.UtcNow;
            var plan = ProgramAssignmentService.PlanEnd(
                SeededProgramResolutionDataSource.GetAssignmentSnapshot(),
                ClientId,
                endsAt,
                now
            );
            if (!plan.IsValid)
            {
                return Task.FromResult(
                    ProgramAssignmentCommandResult.Invalid(plan.ValidationErrors!)
                );
            }

            SeededProgramResolutionDataSource.ApplyPlan(plan);
            RecordAudit(
                plan.ActiveAssignmentId!.Value,
                "ProgramAssignmentEnded",
                actor,
                correlationId,
                now
            );
            return Task.FromResult(ProgramAssignmentCommandResult.Success());
        }
    }

    private void RecordAudit(
        Guid assignmentId,
        string action,
        string actor,
        string correlationId,
        DateTimeOffset occurredAt
    ) =>
        auditEvents.Add(
            new FleetAuditEvent
            {
                Id = Guid.NewGuid(),
                Actor = actor,
                Action = action,
                EntityType = "ClientProgramAssignment",
                EntityId = assignmentId,
                OccurredAt = occurredAt,
                Succeeded = true,
                CorrelationId = correlationId,
            }
        );

    private static FleetCardProgramAssignmentResponse ToResponse(
        ClientProgramAssignment assignment
    ) => new(assignment.Id, assignment.ProgramId, assignment.StartsAt, assignment.EndsAt);
}
