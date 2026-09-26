using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetCarFuelAndAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cars_ClientId_NormalizedRegistrationPlate",
                table: "Cars"
            );

            migrationBuilder.AddColumn<string>(
                name: "FuelType",
                table: "Cars",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                EXEC(N'
                UPDATE [Cars]
                SET [FuelType] = CASE
                    WHEN [Id] = ''11111111-1111-1111-1111-111111111111'' THEN N''Diesel''
                    WHEN [Id] = ''33333333-3333-3333-3333-333333333333'' THEN N''95''
                    ELSE NULL
                END;
                ');
                """
            );

            migrationBuilder.AlterColumn<string>(
                name: "FuelType",
                table: "Cars",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true
            );

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Action = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    EntityType = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    CorrelationId = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ClientId_NormalizedRegistrationPlate",
                table: "Cars",
                columns: new[] { "ClientId", "NormalizedRegistrationPlate" },
                unique: true,
                filter: "[IsArchived] = 0"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_Cars_ClientId_NormalizedRegistrationPlate",
                table: "Cars"
            );

            migrationBuilder.DropColumn(name: "FuelType", table: "Cars");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ClientId_NormalizedRegistrationPlate",
                table: "Cars",
                columns: new[] { "ClientId", "NormalizedRegistrationPlate" },
                unique: true
            );
        }
    }
}
