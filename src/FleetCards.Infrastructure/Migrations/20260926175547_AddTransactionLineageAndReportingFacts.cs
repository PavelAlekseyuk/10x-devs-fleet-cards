using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLineageAndReportingFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrectsTransactionId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "LineageId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PosOccurredAtLocal",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PosTimeZoneId",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Transactions",
                type: "int",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                EXEC(N'
                UPDATE [Transactions]
                SET [LineageId] = [Id],
                    [Version] = 1,
                    [PosTimeZoneId] = N''Europe/Warsaw'',
                    [PosOccurredAtLocal] = SWITCHOFFSET([OccurredAt], ''+00:00'') AT TIME ZONE N''Central European Standard Time'',
                    [OccurredAt] = SWITCHOFFSET([OccurredAt], ''+00:00'');
                ');
                """
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "LineageId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PosOccurredAtLocal",
                table: "Transactions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<string>(
                name: "PosTimeZoneId",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true
            );

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Transactions_Id_LineageId",
                table: "Transactions",
                columns: new[] { "Id", "LineageId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CorrectsTransactionId_LineageId",
                table: "Transactions",
                columns: new[] { "CorrectsTransactionId", "LineageId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LineageId_Version",
                table: "Transactions",
                columns: new[] { "LineageId", "Version" },
                unique: true,
                filter: "[Version] IS NOT NULL"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_LineageVersion",
                table: "Transactions",
                sql: "([Status] = 'Completed' AND [Id] = [LineageId] AND [Version] = 1 AND [CorrectsTransactionId] IS NULL) OR ([Status] = 'Recalculated' AND [Id] <> [LineageId] AND [Version] > 1 AND [CorrectsTransactionId] = [LineageId]) OR ([Status] IN ('Refunded', 'Cancelled') AND [Id] <> [LineageId] AND [Version] IS NULL AND [CorrectsTransactionId] = [LineageId])"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_OccurredAtUtc",
                table: "Transactions",
                sql: "DATEPART(TZOFFSET, [OccurredAt]) = 0"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_CorrectsTransactionId_LineageId",
                table: "Transactions",
                columns: new[] { "CorrectsTransactionId", "LineageId" },
                principalTable: "Transactions",
                principalColumns: new[] { "Id", "LineageId" },
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_CorrectsTransactionId_LineageId",
                table: "Transactions"
            );

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Transactions_Id_LineageId",
                table: "Transactions"
            );

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CorrectsTransactionId_LineageId",
                table: "Transactions"
            );

            migrationBuilder.DropIndex(
                name: "IX_Transactions_LineageId_Version",
                table: "Transactions"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_LineageVersion",
                table: "Transactions"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_OccurredAtUtc",
                table: "Transactions"
            );

            migrationBuilder.DropColumn(name: "CorrectsTransactionId", table: "Transactions");

            migrationBuilder.DropColumn(name: "LineageId", table: "Transactions");

            migrationBuilder.DropColumn(name: "PosOccurredAtLocal", table: "Transactions");

            migrationBuilder.DropColumn(name: "PosTimeZoneId", table: "Transactions");

            migrationBuilder.DropColumn(name: "Version", table: "Transactions");
        }
    }
}
