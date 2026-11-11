using Coordina.Api.Postgres.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Postgres.Tests.Integration.Persistence;

/// <summary>ReplaceMinutesWithCardSizes turns minute estimates into sizes and keeps the data.</summary>
public sealed class CardSizeMigrationTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private const string PreviousMigration = "AddCardCommentEditedAt";

  private static readonly Guid WorkspaceId = Guid.NewGuid();
  private static readonly Guid ProjectId = Guid.NewGuid();
  private static readonly Guid BoardId = Guid.NewGuid();
  private static readonly Guid ListId = Guid.NewGuid();

  [Fact]
  public async Task Migration_ConvertsEstimatesAndFocusSlots()
  {
    await using var database = await Postgres.CreateDatabaseAtMigrationAsync(PreviousMigration);
    await database.InsertWorkspaceWithProjectsAsync(WorkspaceId, ProjectId);
    int?[] estimates = [null, 15, 60, 61, 240, 480, 960, 1440, 2880];
    await database.ExecuteAsync($"""
      INSERT INTO boards (id, workspace_id, project_id, name, template, created_at, updated_at)
      VALUES ('{BoardId}', '{WorkspaceId}', '{ProjectId}', 'Legacy', 'BASIC', now(), now());
      INSERT INTO board_lists (id, board_id, workspace_id, project_id, title, position, task_status, created_at, updated_at)
      VALUES ('{ListId}', '{BoardId}', '{WorkspaceId}', '{ProjectId}', 'Todo', 0, 'TODO', now(), now());
      INSERT INTO board_cards (id, board_id, list_id, workspace_id, project_id, title, labels, estimated_minutes, is_completed, position, created_at, updated_at)
      VALUES {string.Join(", ", estimates.Select((minutes, index) =>
        $"(gen_random_uuid(), '{BoardId}', '{ListId}', '{WorkspaceId}', '{ProjectId}', 'Card {index}', ARRAY[]::text[], {(minutes is null ? "NULL" : minutes)}, FALSE, {index}, now(), now())"))};
      INSERT INTO board_planning_preferences (board_id, user_id, available_minutes, context, prefer_assigned, include_unassigned, updated_at)
      VALUES
        ('{BoardId}', gen_random_uuid(), 0, NULL, TRUE, TRUE, now()),
        ('{BoardId}', gen_random_uuid(), 120, NULL, TRUE, TRUE, now()),
        ('{BoardId}', gen_random_uuid(), 480, NULL, TRUE, TRUE, now());
      """);

    await using (var dbContext = database.CreateDbContext())
    {
      await dbContext.Database.MigrateAsync();
    }

    Assert.Equal(
      "-|XS|XS|S|S|M|M|L|L",
      await database.ScalarAsync<string>(
        $"SELECT string_agg(coalesce(size, '-'), '|' ORDER BY position) FROM board_cards WHERE board_id = '{BoardId}'"));
    Assert.Equal(
      "-|M|S",
      await database.ScalarAsync<string>(
        $"SELECT string_agg(coalesce(max_size, '-'), '|' ORDER BY coalesce(max_size, '-')) FROM board_planning_preferences WHERE board_id = '{BoardId}'"));
    Assert.Equal(0, await database.ScalarAsync<long>(
      """
      SELECT count(*) FROM information_schema.columns
      WHERE (table_name = 'board_cards' AND column_name = 'estimated_minutes')
         OR (table_name = 'board_planning_preferences' AND column_name = 'available_minutes')
      """));
  }
}
