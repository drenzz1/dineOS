using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMenuCategoryFkAndDropTenantAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Tenants: drop the derived aggregate columns ────────────────────
            // TotalOrders / StaffCount / Revenue are computable from Orders,
            // StaffMembers and Payments. They were never maintained (always 0),
            // so dropping them loses no real data; the values are now computed in
            // AdminRestaurantService.
            migrationBuilder.DropColumn(name: "Revenue", table: "Tenants");
            migrationBuilder.DropColumn(name: "StaffCount", table: "Tenants");
            migrationBuilder.DropColumn(name: "TotalOrders", table: "Tenants");

            // ── MenuItems.Category (text) → CategoryId (FK to MenuCategories) ───
            // Done as an expand/backfill/contract so existing rows keep their
            // category. 1) add the FK column nullable, 2) materialise the missing
            // categories and link rows, 3) enforce NOT NULL + FK, 4) drop the old
            // text column and its index.
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId_Category",
                table: "MenuItems");

            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "MenuItems",
                type: "bigint",
                nullable: true);

            // Create a MenuCategory for every distinct (TenantId, Category) name
            // that doesn't already have one.
            migrationBuilder.Sql("""
                INSERT INTO "MenuCategories" ("TenantId", "Name", "CreatedAt")
                SELECT DISTINCT mi."TenantId", mi."Category", now()
                FROM "MenuItems" mi
                WHERE mi."Category" IS NOT NULL AND mi."Category" <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "MenuCategories" mc
                      WHERE mc."TenantId" = mi."TenantId"
                        AND mc."Name" = mi."Category"
                        AND mc."DeletedAt" IS NULL);
                """);

            // Link each item to its category.
            migrationBuilder.Sql("""
                UPDATE "MenuItems" mi
                SET "CategoryId" = mc."Id"
                FROM "MenuCategories" mc
                WHERE mc."TenantId" = mi."TenantId"
                  AND mc."Name" = mi."Category"
                  AND mc."DeletedAt" IS NULL;
                """);

            // Safety net: bucket any item with a blank/unmatched category under
            // "Uncategorized" so the NOT NULL constraint below cannot fail.
            migrationBuilder.Sql("""
                INSERT INTO "MenuCategories" ("TenantId", "Name", "CreatedAt")
                SELECT DISTINCT mi."TenantId", 'Uncategorized', now()
                FROM "MenuItems" mi
                WHERE mi."CategoryId" IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "MenuCategories" mc
                      WHERE mc."TenantId" = mi."TenantId"
                        AND mc."Name" = 'Uncategorized'
                        AND mc."DeletedAt" IS NULL);
                """);
            migrationBuilder.Sql("""
                UPDATE "MenuItems" mi
                SET "CategoryId" = mc."Id"
                FROM "MenuCategories" mc
                WHERE mi."CategoryId" IS NULL
                  AND mc."TenantId" = mi."TenantId"
                  AND mc."Name" = 'Uncategorized'
                  AND mc."DeletedAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "CategoryId",
                table: "MenuItems",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "Category", table: "MenuItems");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId",
                table: "MenuItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_CategoryId",
                table: "MenuItems",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_MenuCategories_CategoryId",
                table: "MenuItems",
                column: "CategoryId",
                principalTable: "MenuCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── MenuItems: CategoryId (FK) → Category (text) ────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_MenuCategories_CategoryId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_CategoryId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId_CategoryId",
                table: "MenuItems");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "MenuItems",
                type: "text",
                nullable: true);

            // Restore the category name from the related MenuCategory before
            // dropping the FK column, so a rollback keeps the data.
            migrationBuilder.Sql("""
                UPDATE "MenuItems" mi
                SET "Category" = mc."Name"
                FROM "MenuCategories" mc
                WHERE mc."Id" = mi."CategoryId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "MenuItems",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "CategoryId", table: "MenuItems");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_Category",
                table: "MenuItems",
                columns: new[] { "TenantId", "Category" });

            // ── Tenants: re-add the derived aggregate columns ──────────────────
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
                columns: new[] { "Revenue", "StaffCount", "TotalOrders" },
                values: new object[] { 0m, 0, 0 });
        }
    }
}
