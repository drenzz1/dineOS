using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantKeycloakUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeycloakUserId",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: 1L,
                column: "KeycloakUserId",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeycloakUserId",
                table: "Tenants");
        }
    }
}
