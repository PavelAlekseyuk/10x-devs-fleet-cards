using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations;

public partial class AddFleetProgramSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClientProgramAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientProgramAssignments", assignment => assignment.Id);
                table.ForeignKey(
                    name: "FK_ClientProgramAssignments_Clients_ClientId",
                    column: assignment => assignment.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_ClientProgramAssignments_Programs_ProgramId",
                    column: assignment => assignment.ProgramId,
                    principalTable: "Programs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "ProgramRules",
            columns: table => new
            {
                ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FuelType = table.Column<string>(
                    type: "nvarchar(20)",
                    maxLength: 20,
                    nullable: false
                ),
                Percent = table.Column<decimal>(
                    type: "decimal(9,4)",
                    precision: 9,
                    scale: 4,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_ProgramRules",
                    rule => new { rule.ProgramId, rule.FuelType }
                );
                table.ForeignKey(
                    name: "FK_ProgramRules_Programs_ProgramId",
                    column: rule => rule.ProgramId,
                    principalTable: "Programs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ClientProgramAssignments_ClientId_StartsAt",
            table: "ClientProgramAssignments",
            columns: new[] { "ClientId", "StartsAt" },
            unique: true
        );
        migrationBuilder.CreateIndex(
            name: "IX_ClientProgramAssignments_ProgramId",
            table: "ClientProgramAssignments",
            column: "ProgramId"
        );

        migrationBuilder.UpdateData(
            table: "Programs",
            keyColumn: "Id",
            keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
            column: "Name",
            value: "Nebula Basic"
        );
        migrationBuilder.InsertData(
            table: "Programs",
            columns: new[] { "Id", "ActiveFrom", "Name" },
            values: new object[,]
            {
                {
                    new Guid("55555555-5555-5555-5555-555555555556"),
                    new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
                    "Orion Diesel"
                },
                {
                    new Guid("55555555-5555-5555-5555-555555555557"),
                    new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
                    "Andromeda Petrol"
                },
                {
                    new Guid("55555555-5555-5555-5555-555555555558"),
                    new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero),
                    "Voyager Balanced"
                },
            }
        );
        migrationBuilder.InsertData(
            table: "ProgramRules",
            columns: new[] { "ProgramId", "FuelType", "Percent" },
            values: new object[,]
            {
                { new Guid("55555555-5555-5555-5555-555555555555"), "Diesel", 0.10m },
                { new Guid("55555555-5555-5555-5555-555555555555"), "Diesel+", 0.10m },
                { new Guid("55555555-5555-5555-5555-555555555555"), "95", -0.02m },
                { new Guid("55555555-5555-5555-5555-555555555555"), "98", -0.02m },
                { new Guid("55555555-5555-5555-5555-555555555555"), "95+", -0.02m },
                { new Guid("55555555-5555-5555-5555-555555555555"), "98+", -0.02m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "Diesel", 0m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "Diesel+", 0m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "95", 0m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "98", 0m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "95+", 0m },
                { new Guid("55555555-5555-5555-5555-555555555556"), "98+", 0m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "Diesel", -0.03m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "Diesel+", -0.03m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "95", 0.09m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "98", 0.09m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "95+", 0.09m },
                { new Guid("55555555-5555-5555-5555-555555555557"), "98+", 0.09m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "Diesel", 0.03m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "Diesel+", 0.03m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "95", 0.02m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "98", 0.02m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "95+", 0.02m },
                { new Guid("55555555-5555-5555-5555-555555555558"), "98+", 0.02m },
            }
        );
        migrationBuilder.InsertData(
            table: "ClientProgramAssignments",
            columns: new[] { "Id", "ClientId", "ProgramId", "StartsAt" },
            values: new object[]
            {
                new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                new Guid("55555555-5555-5555-5555-555555555555"),
                new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
            }
        );

        // Forward-only data transformation: Down reverts schema/catalog changes but cannot restore
        // prior transaction discounts or totals. Back up the database before applying this migration.
        migrationBuilder.UpdateData(
            table: "Transactions",
            keyColumn: "Id",
            keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
            columns: new[] { "Discount", "NetTotal", "VatAmount", "GrossTotal" },
            values: new object[] { 0m, 273.11m, 62.82m, 335.93m }
        );
        migrationBuilder.Sql(
            """
            UPDATE [Transactions]
            SET [Discount] = 0,
                [NetTotal] = ROUND([Quantity] * [UnitPrice], 2),
                [VatAmount] = ROUND(ROUND([Quantity] * [UnitPrice], 2) * [VatRate], 2),
                [GrossTotal] = ROUND([Quantity] * [UnitPrice], 2)
                    + ROUND(ROUND([Quantity] * [UnitPrice], 2) * [VatRate], 2);
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Forward-only data transformation: Down reverts schema/catalog changes but cannot restore
        // prior transaction discounts or totals. Back up the database before applying this migration.
        migrationBuilder.DropTable(name: "ClientProgramAssignments");
        migrationBuilder.DropTable(name: "ProgramRules");

        migrationBuilder.DeleteData(
            table: "Programs",
            keyColumn: "Id",
            keyValues: new object[]
            {
                new Guid("55555555-5555-5555-5555-555555555556"),
                new Guid("55555555-5555-5555-5555-555555555557"),
                new Guid("55555555-5555-5555-5555-555555555558"),
            }
        );
        migrationBuilder.UpdateData(
            table: "Programs",
            keyColumn: "Id",
            keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
            column: "Name",
            value: "Baltic Standard"
        );
    }
}
