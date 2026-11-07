using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class OneBoardPerProject : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Keep the oldest board of each project, which is the only one the web
      // app shows. The others were unreachable; their lists and cards cascade.
      migrationBuilder.Sql("""
                DELETE FROM boards AS newer
                USING boards AS older
                WHERE newer.project_id = older.project_id
                  AND (older.created_at, older.id) < (newer.created_at, newer.id);
                """);

      migrationBuilder.DropIndex(
          name: "IX_boards_project_id",
          table: "boards");

      migrationBuilder.CreateIndex(
          name: "IX_boards_project_id",
          table: "boards",
          column: "project_id",
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_boards_project_id",
          table: "boards");

      migrationBuilder.CreateIndex(
          name: "IX_boards_project_id",
          table: "boards",
          column: "project_id");
    }
  }
}
