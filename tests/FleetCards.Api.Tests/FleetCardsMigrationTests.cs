using FleetCards.Domain;
using FleetCards.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace FleetCards.Api.Tests;

public sealed class FleetCardsMigrationTests
{
    private const string MigrationBeforeAssignmentRequired =
        "20260926195450_ReconcileSeededTransactionQuantity";
    private static readonly Guid ClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AssignedCardId = Guid.Parse(
        "22222222-2222-2222-2222-222222222222"
    );
    private static readonly Guid AssignedCarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegacyCardId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid LegacyPurchaseId = Guid.Parse(
        "88888888-8888-8888-8888-888888888801"
    );
    private static readonly Guid AssignedCardAuditId = Guid.Parse(
        "99999999-9999-9999-9999-999999999999"
    );

    [Fact]
    public async Task Assignment_required_migration_removes_only_unassigned_card_facts()
    {
        await using var sqlServer = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04"
        )
            .WithPassword("FleetCards_migration_Test123!")
            .Build();
        await sqlServer.StartAsync();

        var connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = $"FleetCardsMigration_{Guid.NewGuid():N}",
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<FleetCardsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new FleetCardsDbContext(options);
        await context.Database.MigrateAsync(MigrationBeforeAssignmentRequired);
        await InsertLegacyFactsAsync(context);

        await context.Database.MigrateAsync("20261005172757_AddFleetProgramSelection");

        Assert.Empty(await context.Cards.Where(card => card.Id == LegacyCardId).ToArrayAsync());
        Assert.Empty(
            await context
                .Transactions.Where(transaction => transaction.CardId == LegacyCardId)
                .ToArrayAsync()
        );
        Assert.Empty(
            await context
                .CardAssignmentEvents.Where(assignment => assignment.CardId == LegacyCardId)
                .ToArrayAsync()
        );
        Assert.Empty(
            await context
                .AuditEvents.Where(audit =>
                    audit.EntityType == "FleetCard" && audit.EntityId == LegacyCardId
                )
                .ToArrayAsync()
        );

        var assignedCard = await context.Cards.SingleAsync(card => card.Id == AssignedCardId);
        Assert.Equal(AssignedCarId, assignedCard.AssignedCarId);
        Assert.Equal(
            1,
            await context.Transactions.CountAsync(transaction =>
                transaction.CardId == AssignedCardId
            )
        );
        Assert.Equal(
            1,
            await context.CardAssignmentEvents.CountAsync(assignment =>
                assignment.CardId == AssignedCardId
            )
        );
        Assert.Contains(
            await context.AuditEvents.ToArrayAsync(),
            audit => audit.Id == AssignedCardAuditId && audit.EntityId == AssignedCardId
        );

        var assignmentIsNullable = await context
            .Database.SqlQueryRaw<bool>(
                """
                SELECT CONVERT(bit, [is_nullable]) AS [Value]
                FROM [sys].[columns]
                WHERE [object_id] = OBJECT_ID(N'[dbo].[Cards]')
                    AND [name] = N'AssignedCarId'
                """
            )
            .SingleAsync();
        Assert.False(assignmentIsNullable);
        Assert.Equal(
            "20261005172757_AddFleetProgramSelection",
            (await context.Database.GetAppliedMigrationsAsync()).Last()
        );
    }

    [Fact]
    public async Task Program_selection_migration_corrects_catalog_and_clears_transactions()
    {
        await using var sqlServer = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04"
        )
            .WithPassword("FleetCards_migration_Test123!")
            .Build();
        await sqlServer.StartAsync();

        var connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = $"FleetCardsProgramMigration_{Guid.NewGuid():N}",
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<FleetCardsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new FleetCardsDbContext(options);
        await context.Database.MigrateAsync(MigrationBeforeAssignmentRequired);

        var legacyTransactionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        context.Transactions.Add(
            CreateTransaction(
                legacyTransactionId,
                legacyTransactionId,
                1,
                null,
                TransactionStatus.Completed,
                10m,
                47.50m,
                10.93m,
                58.43m,
                cardId: AssignedCardId
            )
        );
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var programs = await context.Programs.OrderBy(program => program.Name).ToArrayAsync();
        Assert.Equal(
            new[] { "Andromeda Petrol", "Nebula Basic", "Orion Diesel", "Voyager Balanced" },
            programs.Select(program => program.Name).ToArray()
        );
        Assert.Equal(
            "Orion Diesel",
            programs.Single(program =>
                program.Id == Guid.Parse("55555555-5555-5555-5555-555555555555")
            ).Name
        );
        Assert.Equal(
            "Nebula Basic",
            programs.Single(program =>
                program.Id == Guid.Parse("55555555-5555-5555-5555-555555555556")
            ).Name
        );
        Assert.DoesNotContain(programs, program => program.Name == "Baltic Standard");

        var rules = await context.ProgramRules.ToDictionaryAsync(
            rule => (rule.ProgramId, rule.FuelType),
            rule => rule.Percent
        );
        var dieselFuelTypes = new[] { "Diesel", "Diesel+" };
        var petrolFuelTypes = new[] { "95", "98", "95+", "98+" };
        var expectedRules = dieselFuelTypes
            .Select(fuelType =>
                (Guid.Parse("55555555-5555-5555-5555-555555555555"), fuelType, 0.10m)
            )
            .Concat(
                petrolFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555555"), fuelType, -0.02m)
                )
            )
            .Concat(
                dieselFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555556"), fuelType, 0m)
                )
            )
            .Concat(
                petrolFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555556"), fuelType, 0m)
                )
            )
            .Concat(
                dieselFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555557"), fuelType, -0.03m)
                )
            )
            .Concat(
                petrolFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555557"), fuelType, 0.09m)
                )
            )
            .Concat(
                dieselFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555558"), fuelType, 0.03m)
                )
            )
            .Concat(
                petrolFuelTypes.Select(fuelType =>
                    (Guid.Parse("55555555-5555-5555-5555-555555555558"), fuelType, 0.02m)
                )
            )
            .ToDictionary(
                expectedRule => (expectedRule.Item1, expectedRule.Item2),
                expectedRule => expectedRule.Item3
            );
        Assert.Equal(expectedRules.Count, rules.Count);
        foreach (var expectedRule in expectedRules)
        {
            Assert.Equal(expectedRule.Value, rules[expectedRule.Key]);
        }

        var assignment = await context.ClientProgramAssignments.SingleAsync();
        Assert.Equal(ClientId, assignment.ClientId);
        Assert.Equal(Guid.Parse("55555555-5555-5555-5555-555555555556"), assignment.ProgramId);
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero), assignment.StartsAt);
        Assert.Null(assignment.EndsAt);

        var transactions = await context.Transactions.ToArrayAsync();
        Assert.Empty(transactions);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static async Task InsertLegacyFactsAsync(FleetCardsDbContext context)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Cards]
                ([Id], [ClientId], [MaskedPan], [ProviderToken], [State], [AssignedCarId])
            VALUES
                ({LegacyCardId}, {ClientId}, {"**** **** **** 8888"}, {"migration-legacy-8888"}, {"Active"}, NULL)
            """
        );

        var purchase = CreateTransaction(
            LegacyPurchaseId,
            LegacyPurchaseId,
            1,
            null,
            TransactionStatus.Completed,
            10m,
            50m,
            11.50m,
            61.50m
        );
        context.Transactions.Add(purchase);
        await context.SaveChangesAsync();

        context.Transactions.AddRange(
            CreateTransaction(
                Guid.Parse("88888888-8888-8888-8888-888888888802"),
                LegacyPurchaseId,
                2,
                LegacyPurchaseId,
                TransactionStatus.Recalculated,
                10m,
                49m,
                11.27m,
                60.27m
            ),
            CreateTransaction(
                Guid.Parse("88888888-8888-8888-8888-888888888803"),
                LegacyPurchaseId,
                null,
                LegacyPurchaseId,
                TransactionStatus.Refunded,
                -1m,
                -5m,
                -1.15m,
                -6.15m
            ),
            CreateTransaction(
                Guid.Parse("88888888-8888-8888-8888-888888888804"),
                LegacyPurchaseId,
                null,
                LegacyPurchaseId,
                TransactionStatus.Cancelled,
                -10m,
                -50m,
                -11.50m,
                -61.50m
            )
        );
        context.CardAssignmentEvents.Add(
            new FleetCardAssignmentEvent
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888805"),
                CardId = LegacyCardId,
                VehicleId = AssignedCarId,
                EffectiveAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
                OrderingKey = 1,
            }
        );
        context.AuditEvents.AddRange(
            new FleetAuditEvent
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888806"),
                Actor = "migration-test",
                Action = "CardIssued",
                EntityType = "FleetCard",
                EntityId = LegacyCardId,
                OccurredAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
                Succeeded = true,
                CorrelationId = "migration-legacy-issue",
            },
            new FleetAuditEvent
            {
                Id = AssignedCardAuditId,
                Actor = "migration-test",
                Action = "CardStateChangedToBlocked",
                EntityType = "FleetCard",
                EntityId = AssignedCardId,
                OccurredAt = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero),
                Succeeded = true,
                CorrelationId = "migration-assigned-state",
            }
        );
        await context.SaveChangesAsync();
    }

    private static FuelTransaction CreateTransaction(
        Guid id,
        Guid lineageId,
        int? version,
        Guid? correctsTransactionId,
        TransactionStatus status,
        decimal quantity,
        decimal netTotal,
        decimal vatAmount,
        decimal grossTotal,
        Guid? cardId = null
    ) =>
        new()
        {
            Id = id,
            LineageId = lineageId,
            Version = version,
            CorrectsTransactionId = correctsTransactionId,
            CardId = cardId ?? LegacyCardId,
            VehicleId = AssignedCarId,
            VehiclePlate = "WA 12345",
            OccurredAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            PosOccurredAtLocal = new DateTimeOffset(2026, 8, 1, 14, 0, 0, TimeSpan.FromHours(2)),
            PosTimeZoneId = "Europe/Warsaw",
            StationName = "Migration fixture",
            FuelType = "Diesel",
            Quantity = quantity,
            UnitPrice = 5m,
            Discount = 0m,
            NetTotal = netTotal,
            VatRate = 0.23m,
            VatAmount = vatAmount,
            GrossTotal = grossTotal,
            Status = status,
        };
}
