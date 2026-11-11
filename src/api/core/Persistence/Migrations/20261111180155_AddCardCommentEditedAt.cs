using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddCardCommentEditedAt : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<DateTimeOffset>(
          name: "edited_at",
          table: "board_card_comments",
          type: "timestamp with time zone",
          nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "edited_at",
          table: "board_card_comments");
    }
  }
}
