using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DineOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingsApiKey",
                table: "PlatformAiSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingsProvider",
                table: "PlatformAiSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "MenuItems",
                type: "vector(768)",
                nullable: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_MenuItems_Embedding\" " +
                "ON \"MenuItems\" USING hnsw (\"Embedding\" vector_cosine_ops) " +
                "WHERE \"Embedding\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingsApiKey",
                table: "PlatformAiSettings");

            migrationBuilder.DropColumn(
                name: "EmbeddingsProvider",
                table: "PlatformAiSettings");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MenuItems_Embedding\";");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "MenuItems");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
