using FleetCards.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    [DbContext(typeof(FleetCardsDbContext))]
    [Migration("20260926120000_AddFleetCardAssignmentEvents")]
    public partial class AddFleetCardAssignmentEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardAssignmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(
                        type: "datetimeoffset",
                        nullable: false
                    ),
                    OrderingKey = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardAssignmentEvents", row => row.Id);
                    table.ForeignKey(
                        name: "FK_CardAssignmentEvents_Cards_CardId",
                        column: row => row.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CardAssignmentEvents_Cars_VehicleId",
                        column: row => row.VehicleId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CardAssignmentEvents_CardId_EffectiveAt_OrderingKey",
                table: "CardAssignmentEvents",
                columns: new[] { "CardId", "EffectiveAt", "OrderingKey" }
            );
            migrationBuilder.CreateIndex(
                name: "IX_CardAssignmentEvents_CardId_OrderingKey",
                table: "CardAssignmentEvents",
                columns: new[] { "CardId", "OrderingKey" },
                unique: true
            );
            migrationBuilder.CreateIndex(
                name: "IX_CardAssignmentEvents_VehicleId",
                table: "CardAssignmentEvents",
                column: "VehicleId"
            );

            migrationBuilder.Sql(
                """
                INSERT INTO [CardAssignmentEvents]
                    ([Id], [CardId], [VehicleId], [EffectiveAt], [OrderingKey])
                SELECT
                    NEWID(),
                    [card].[Id],
                    [card].[AssignedCarId],
                    COALESCE(MIN([transaction].[OccurredAt]), CONVERT(datetimeoffset, '2000-01-01T00:00:00+00:00')),
                    1
                FROM [Cards] AS [card]
                LEFT JOIN [Transactions] AS [transaction] ON [transaction].[CardId] = [card].[Id]
                WHERE [card].[AssignedCarId] IS NOT NULL
                    AND [card].[Id] <> '22222222-2222-2222-2222-222222222222'
                GROUP BY [card].[Id], [card].[AssignedCarId];

                INSERT INTO [CardAssignmentEvents]
                    ([Id], [CardId], [VehicleId], [EffectiveAt], [OrderingKey])
                VALUES
                    ('66666666-6666-6666-6666-666666666666',
                     '22222222-2222-2222-2222-222222222222',
                     '11111111-1111-1111-1111-111111111111',
                     '2026-09-09T12:00:00+00:00',
                     1);
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CardAssignmentEvents");
        }
    }
}
