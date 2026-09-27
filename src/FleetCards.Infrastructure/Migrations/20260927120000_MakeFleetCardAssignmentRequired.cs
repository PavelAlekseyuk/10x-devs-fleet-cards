using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations;

[DbContext(typeof(FleetCardsDbContext))]
[Migration("20260927120000_MakeFleetCardAssignmentRequired")]
public partial class MakeFleetCardAssignmentRequired : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @CardsToDelete TABLE (Id uniqueidentifier PRIMARY KEY);

            INSERT INTO @CardsToDelete (Id)
            SELECT [Id]
            FROM [Cards]
            WHERE [AssignedCarId] IS NULL;

            IF EXISTS
            (
                SELECT 1
                FROM [Transactions] AS [correction]
                INNER JOIN [Transactions] AS [original]
                    ON [correction].[CorrectsTransactionId] = [original].[Id]
                    AND [correction].[LineageId] = [original].[LineageId]
                INNER JOIN @CardsToDelete AS [deletedCard]
                    ON [original].[CardId] = [deletedCard].[Id]
                LEFT JOIN @CardsToDelete AS [correctionCard]
                    ON [correction].[CardId] = [correctionCard].[Id]
                WHERE [correctionCard].[Id] IS NULL
            )
            BEGIN
                THROW 51000, 'Cannot remove unassigned cards because an assigned-card transaction correction references one of their transactions.', 1;
            END;

            DELETE [auditEvent]
            FROM [AuditEvents] AS [auditEvent]
            INNER JOIN @CardsToDelete AS [card]
                ON [auditEvent].[EntityId] = [card].[Id]
            WHERE [auditEvent].[EntityType] = N'FleetCard';

            DELETE [assignment]
            FROM [CardAssignmentEvents] AS [assignment]
            INNER JOIN @CardsToDelete AS [card]
                ON [assignment].[CardId] = [card].[Id];

            DELETE [correction]
            FROM [Transactions] AS [correction]
            INNER JOIN @CardsToDelete AS [card]
                ON [correction].[CardId] = [card].[Id]
            WHERE [correction].[CorrectsTransactionId] IS NOT NULL;

            DELETE [transaction]
            FROM [Transactions] AS [transaction]
            INNER JOIN @CardsToDelete AS [card]
                ON [transaction].[CardId] = [card].[Id]
            WHERE [transaction].[CorrectsTransactionId] IS NULL;

            DELETE [card]
            FROM [Cards] AS [card]
            INNER JOIN @CardsToDelete AS [deletedCard]
                ON [card].[Id] = [deletedCard].[Id];
            """
        );

        migrationBuilder.DropIndex(name: "IX_Cards_AssignedCarId", table: "Cards");

        migrationBuilder.AlterColumn<Guid>(
            name: "AssignedCarId",
            table: "Cards",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_Cards_AssignedCarId",
            table: "Cards",
            column: "AssignedCarId"
        );

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM [AuditEvents]
                WHERE [Id] = '77777777-7777-7777-7777-777777777777'
            )
            BEGIN
                INSERT INTO [AuditEvents]
                    ([Id], [Actor], [Action], [EntityType], [EntityId], [OccurredAt], [Succeeded], [CorrelationId])
                VALUES
                    ('77777777-7777-7777-7777-777777777777', N'system', N'CardIssued', N'FleetCard',
                     '22222222-2222-2222-2222-222222222222', '2026-09-09T12:00:00+00:00', 1, N'seeded-card-issue');
            END;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DELETE FROM [AuditEvents] WHERE [Id] = '77777777-7777-7777-7777-777777777777';"
        );

        migrationBuilder.DropIndex(name: "IX_Cards_AssignedCarId", table: "Cards");

        migrationBuilder.AlterColumn<Guid>(
            name: "AssignedCarId",
            table: "Cards",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Cards_AssignedCarId",
            table: "Cards",
            column: "AssignedCarId"
        );
    }
}
