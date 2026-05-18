using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStripeSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeSessionId",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: 1L,
                column: "StripeSessionId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeSessionId",
                table: "Tenants",
                column: "StripeSessionId",
                filter: "\"StripeSessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeSessionId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeSessionId",
                table: "Tenants");
        }
    }
}
