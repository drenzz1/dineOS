using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsTenantCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_CreatedAt",
                table: "Payments",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_CreatedAt",
                table: "Payments");
        }
    }
}
