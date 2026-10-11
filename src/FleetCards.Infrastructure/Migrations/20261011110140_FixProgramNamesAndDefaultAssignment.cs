using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProgramNamesAndDefaultAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ClientProgramAssignments",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                column: "ProgramId",
                value: new Guid("55555555-5555-5555-5555-555555555556"));

            migrationBuilder.UpdateData(
                table: "Programs",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "Name",
                value: "Orion Diesel");

            migrationBuilder.UpdateData(
                table: "Programs",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555556"),
                column: "Name",
                value: "Nebula Basic");

            // Remove demo transactions so new data reflects the corrected default program.
            migrationBuilder.Sql("DELETE FROM [Transactions];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Transactions removed by Up are demo data and cannot be restored on rollback.
            migrationBuilder.UpdateData(
                table: "ClientProgramAssignments",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                column: "ProgramId",
                value: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.UpdateData(
                table: "Programs",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "Name",
                value: "Nebula Basic");

            migrationBuilder.UpdateData(
                table: "Programs",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555556"),
                column: "Name",
                value: "Orion Diesel");
        }
    }
}
