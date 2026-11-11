using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <summary>
  /// Cards get a size (XS to XXL) instead of an estimate in minutes, and the
  /// planning preference keeps the biggest size that fits a focus slot instead
  /// of a number of minutes. Existing values are converted, then dropped.
  /// </summary>
  public partial class ReplaceMinutesWithCardSizes : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropCheckConstraint(
        name: "ck_board_cards_estimated_minutes",
        table: "board_cards");

      migrationBuilder.AddColumn<string>(
        name: "size",
        table: "board_cards",
        type: "character varying(4)",
        maxLength: 4,
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "max_size",
        table: "board_planning_preferences",
        type: "character varying(4)",
        maxLength: 4,
        nullable: true);

      // The thresholds of issue #130, in minutes of focus time.
      migrationBuilder.Sql(
        """
        UPDATE board_cards SET size = CASE
          WHEN estimated_minutes <= 60 THEN 'XS'
          WHEN estimated_minutes <= 240 THEN 'S'
          WHEN estimated_minutes <= 960 THEN 'M'
          WHEN estimated_minutes <= 2880 THEN 'L'
          WHEN estimated_minutes <= 5760 THEN 'XL'
          ELSE 'XXL'
        END
        WHERE estimated_minutes IS NOT NULL;
        """);

      // The focus slots the planning panel now offers: a quick task (up to S)
      // or half a day (up to M); 0 meant no limit.
      migrationBuilder.Sql(
        """
        UPDATE board_planning_preferences SET max_size = CASE
          WHEN available_minutes = 0 THEN NULL
          WHEN available_minutes <= 240 THEN 'S'
          ELSE 'M'
        END;
        """);

      migrationBuilder.DropColumn(
        name: "estimated_minutes",
        table: "board_cards");

      migrationBuilder.DropColumn(
        name: "available_minutes",
        table: "board_planning_preferences");

      migrationBuilder.AddCheckConstraint(
        name: "ck_board_cards_size",
        table: "board_cards",
        sql: "size IS NULL OR size IN ('XS', 'S', 'M', 'L', 'XL', 'XXL')");

      migrationBuilder.AddCheckConstraint(
        name: "ck_board_planning_preferences_max_size",
        table: "board_planning_preferences",
        sql: "max_size IS NULL OR max_size IN ('XS', 'S', 'M', 'L', 'XL', 'XXL')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropCheckConstraint(
        name: "ck_board_cards_size",
        table: "board_cards");

      migrationBuilder.DropCheckConstraint(
        name: "ck_board_planning_preferences_max_size",
        table: "board_planning_preferences");

      migrationBuilder.AddColumn<int>(
        name: "estimated_minutes",
        table: "board_cards",
        type: "integer",
        nullable: true);

      migrationBuilder.AddColumn<int>(
        name: "available_minutes",
        table: "board_planning_preferences",
        type: "integer",
        nullable: false,
        defaultValue: 0);

      migrationBuilder.Sql(
        """
        UPDATE board_cards SET estimated_minutes = CASE size
          WHEN 'XS' THEN 30
          WHEN 'S' THEN 120
          WHEN 'M' THEN 480
          WHEN 'L' THEN 1440
          WHEN 'XL' THEN 2880
          WHEN 'XXL' THEN 2880
        END
        WHERE size IS NOT NULL;
        """);

      migrationBuilder.Sql(
        """
        UPDATE board_planning_preferences SET available_minutes = CASE max_size
          WHEN 'XS' THEN 60
          WHEN 'S' THEN 120
          WHEN 'M' THEN 240
          ELSE 0
        END
        WHERE max_size IS NOT NULL;
        """);

      migrationBuilder.DropColumn(
        name: "size",
        table: "board_cards");

      migrationBuilder.DropColumn(
        name: "max_size",
        table: "board_planning_preferences");

      migrationBuilder.AddCheckConstraint(
        name: "ck_board_cards_estimated_minutes",
        table: "board_cards",
        sql: "estimated_minutes IS NULL OR estimated_minutes BETWEEN 5 AND 2880");
    }
  }
}
