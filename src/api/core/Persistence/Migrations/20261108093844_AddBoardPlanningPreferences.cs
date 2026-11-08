using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddBoardPlanningPreferences : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "board_planning_preferences",
          columns: table => new
          {
            board_id = table.Column<Guid>(type: "uuid", nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            available_minutes = table.Column<int>(type: "integer", nullable: false),
            context = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
            prefer_assigned = table.Column<bool>(type: "boolean", nullable: false),
            include_unassigned = table.Column<bool>(type: "boolean", nullable: false),
            updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_board_planning_preferences", x => new { x.board_id, x.user_id });
            table.ForeignKey(
                      name: "FK_board_planning_preferences_boards_board_id",
                      column: x => x.board_id,
                      principalTable: "boards",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_board_planning_preferences_user_id",
          table: "board_planning_preferences",
          column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "board_planning_preferences");
    }
  }
}
