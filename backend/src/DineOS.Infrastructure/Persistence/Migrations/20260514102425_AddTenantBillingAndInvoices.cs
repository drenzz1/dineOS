using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBillingAndInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingStatus",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    StripeInvoiceId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HostedInvoiceUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvoices", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "BillingCycle", "BillingStatus", "CurrentPeriodEnd", "StripeCustomerId", "StripeSubscriptionId" },
                values: new object[] { null, 0, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeCustomerId",
                table: "Tenants",
                column: "StripeCustomerId",
                unique: true,
                filter: "\"StripeCustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StripeSubscriptionId",
                table: "Tenants",
                column: "StripeSubscriptionId",
                unique: true,
                filter: "\"StripeSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvoices_StripeInvoiceId",
                table: "TenantInvoices",
                column: "StripeInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvoices_TenantId",
                table: "TenantInvoices",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_StripeSubscriptionId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BillingStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Tenants");
        }
    }
}
