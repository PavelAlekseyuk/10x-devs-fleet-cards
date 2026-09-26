using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileSeededTransactionQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "Quantity",
                value: 42.081m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "Quantity",
                value: 42.125m);
        }
    }
}
