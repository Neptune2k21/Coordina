using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddBoardHistory : Migration
  {
    private static readonly string[] EventPageColumns = ["board_id", "id"];

    // The database writes the history itself: cards change through EF, bulk
    // updates and cascades, and a write that forgot to record its event
    // would silently teach the forecasts something false.
    //
    // A board being deleted takes its cards with it; by then it is gone, and
    // so is the history it would be recorded in.
    private const string RecordFunction = """
            CREATE FUNCTION record_board_card_event(
              event_board uuid,
              event_card uuid,
              event_kind text,
              event_from text,
              event_to text,
              event_at timestamptz)
            RETURNS void AS $$
              INSERT INTO board_card_events (board_id, card_id, kind, from_value, to_value, occurred_at)
              SELECT event_board, event_card, event_kind, event_from, event_to, event_at
              WHERE EXISTS (SELECT 1 FROM boards WHERE id = event_board);
            $$ LANGUAGE sql;
            """;

    // A card's task status is its list's.
    private const string CardTrigger = """
            CREATE FUNCTION record_board_card_change() RETURNS trigger AS $$
            DECLARE
              old_status text;
              new_status text;
            BEGIN
              IF TG_OP = 'DELETE' THEN
                PERFORM record_board_card_event(OLD.board_id, OLD.id, 'DELETED', NULL, NULL, now());
                RETURN NULL;
              END IF;

              SELECT task_status INTO new_status FROM board_lists WHERE id = NEW.list_id;

              IF TG_OP = 'INSERT' THEN
                PERFORM record_board_card_event(NEW.board_id, NEW.id, 'CREATED', NULL, new_status, NEW.created_at);
                RETURN NULL;
              END IF;

              IF NEW.list_id <> OLD.list_id THEN
                SELECT task_status INTO old_status FROM board_lists WHERE id = OLD.list_id;

                IF new_status IS DISTINCT FROM old_status THEN
                  PERFORM record_board_card_event(
                    NEW.board_id, NEW.id, 'STATUS_CHANGED', old_status, new_status, NEW.updated_at);
                END IF;
              END IF;

              IF NEW.size IS DISTINCT FROM OLD.size THEN
                PERFORM record_board_card_event(
                  NEW.board_id, NEW.id, 'SIZE_CHANGED', OLD.size, NEW.size, NEW.updated_at);
              END IF;

              IF NEW.milestone_date IS DISTINCT FROM OLD.milestone_date THEN
                PERFORM record_board_card_event(
                  NEW.board_id, NEW.id, 'MILESTONE_CHANGED',
                  OLD.milestone_date::text, NEW.milestone_date::text, NEW.updated_at);
              END IF;

              RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER board_card_history
            AFTER INSERT OR DELETE OR UPDATE OF list_id, size, milestone_date ON board_cards
            FOR EACH ROW EXECUTE FUNCTION record_board_card_change();
            """;

    private const string ListTrigger = """
            CREATE FUNCTION record_board_list_change() RETURNS trigger AS $$
            BEGIN
              PERFORM record_board_card_event(
                card.board_id, card.id, 'STATUS_CHANGED', OLD.task_status, NEW.task_status, NEW.updated_at)
              FROM board_cards card
              WHERE card.list_id = NEW.id;
              RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER board_list_history
            AFTER UPDATE OF task_status ON board_lists
            FOR EACH ROW WHEN (OLD.task_status IS DISTINCT FROM NEW.task_status)
            EXECUTE FUNCTION record_board_list_change();
            """;

    // Deleting a card deletes its links and assignees: the card's own event
    // says so, and by then it is no longer there to name their board.
    private const string DependencyTrigger = """
            CREATE FUNCTION record_board_card_dependency_change() RETURNS trigger AS $$
            BEGIN
              IF TG_OP = 'INSERT' THEN
                PERFORM record_board_card_event(
                  card.board_id, card.id, 'DEPENDENCY_ADDED', NULL, NEW.depends_on_card_id::text, NEW.created_at)
                FROM board_cards card
                WHERE card.id = NEW.card_id;
              ELSE
                PERFORM record_board_card_event(
                  card.board_id, card.id, 'DEPENDENCY_REMOVED', OLD.depends_on_card_id::text, NULL, now())
                FROM board_cards card
                WHERE card.id = OLD.card_id;
              END IF;

              RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER board_card_dependency_history
            AFTER INSERT OR DELETE ON board_card_dependencies
            FOR EACH ROW EXECUTE FUNCTION record_board_card_dependency_change();
            """;

    private const string AssigneeTrigger = """
            CREATE FUNCTION record_board_card_assignee_change() RETURNS trigger AS $$
            BEGIN
              IF TG_OP = 'INSERT' THEN
                PERFORM record_board_card_event(
                  card.board_id, card.id, 'ASSIGNEE_ADDED', NULL, NEW.user_id::text, NEW.assigned_at)
                FROM board_cards card
                WHERE card.id = NEW.card_id;
              ELSE
                PERFORM record_board_card_event(
                  card.board_id, card.id, 'ASSIGNEE_REMOVED', OLD.user_id::text, NULL, now())
                FROM board_cards card
                WHERE card.id = OLD.card_id;
              END IF;

              RETURN NULL;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER board_card_assignee_history
            AFTER INSERT OR DELETE ON board_card_assignees
            FOR EACH ROW EXECUTE FUNCTION record_board_card_assignee_change();
            """;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "board_card_events",
          columns: table => new
          {
            id = table.Column<long>(type: "bigint", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
            board_id = table.Column<Guid>(type: "uuid", nullable: false),
            card_id = table.Column<Guid>(type: "uuid", nullable: false),
            kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            from_value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            to_value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_board_card_events", x => x.id);
            table.ForeignKey(
                      name: "FK_board_card_events_boards_board_id",
                      column: x => x.board_id,
                      principalTable: "boards",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "board_forecast_snapshots",
          columns: table => new
          {
            milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
            taken_on = table.Column<DateOnly>(type: "date", nullable: false),
            board_id = table.Column<Guid>(type: "uuid", nullable: false),
            target_date = table.Column<DateOnly>(type: "date", nullable: false),
            forecast_date = table.Column<DateOnly>(type: "date", nullable: false),
            commit_date = table.Column<DateOnly>(type: "date", nullable: false),
            on_time = table.Column<double>(type: "double precision", nullable: false),
            open_card_count = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_board_forecast_snapshots", x => new { x.milestone_id, x.taken_on });
            table.ForeignKey(
                      name: "FK_board_forecast_snapshots_boards_board_id",
                      column: x => x.board_id,
                      principalTable: "boards",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_board_card_events_board_id_id",
          table: "board_card_events",
          columns: EventPageColumns);

      migrationBuilder.CreateIndex(
          name: "IX_board_forecast_snapshots_board_id",
          table: "board_forecast_snapshots",
          column: "board_id");

      migrationBuilder.Sql(RecordFunction);
      migrationBuilder.Sql(CardTrigger);
      migrationBuilder.Sql(ListTrigger);
      migrationBuilder.Sql(DependencyTrigger);
      migrationBuilder.Sql(AssigneeTrigger);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.Sql("""
                DROP TRIGGER board_card_history ON board_cards;
                DROP TRIGGER board_list_history ON board_lists;
                DROP TRIGGER board_card_dependency_history ON board_card_dependencies;
                DROP TRIGGER board_card_assignee_history ON board_card_assignees;
                DROP FUNCTION record_board_card_change();
                DROP FUNCTION record_board_list_change();
                DROP FUNCTION record_board_card_dependency_change();
                DROP FUNCTION record_board_card_assignee_change();
                DROP FUNCTION record_board_card_event(uuid, uuid, text, text, text, timestamptz);
                """);

      migrationBuilder.DropTable(
          name: "board_card_events");

      migrationBuilder.DropTable(
          name: "board_forecast_snapshots");
    }
  }
}
