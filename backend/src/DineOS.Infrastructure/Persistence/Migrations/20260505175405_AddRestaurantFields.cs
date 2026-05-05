using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Revenue",
                table: "Tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "StaffCount",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalOrders",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "City", "OwnerEmail", "OwnerName", "Phone", "Plan", "Revenue", "StaffCount", "TotalOrders" },
                values: new object[] { "Tirana", "owner@demo-restaurant.com", "Demo Owner", "+1 555 000 0000", 1, 0m, 0, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Revenue",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StaffCount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TotalOrders",
                table: "Tenants");
        }
    }
}
