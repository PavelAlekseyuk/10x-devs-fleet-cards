using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetCards.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeededUserCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                column: "PasswordHash",
                value: "pbkdf2-sha256:120000:32:YW5uYS5ub3dha3xmbGVldGNhcmRzfHYx:ii5CUBhy/r7ijfx9rxha1fx84ADBy5blrUopRVSUQOE=");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "PasswordHash", "Permission", "UserName" },
                values: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), "pbkdf2-sha256:120000:32:cGlvdHIud2lzbmlld3NraXxmbGVldGNhcmRzfHYx:Jey8SEvmKOAL0FlmRyh51nW+zGLSsICzEqjEb9w9bgQ=", "ReadOnly", "piotr.wisniewski" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");
        }
    }
}
