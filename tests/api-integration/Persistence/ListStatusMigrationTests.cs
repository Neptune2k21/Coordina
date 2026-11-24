using Coordina.Api.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Integration.Tests.Persistence;

/// <summary>AddBoardListTaskStatus gives every list a status and aligns its cards' completion.</summary>
public sealed class ListStatusMigrationTests
{
  private static readonly Guid WorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
  private static readonly Guid ProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
  private static readonly Guid ScrumProjectId = Guid.Parse("20000000-0000-0000-0000-000000000002");
  private static readonly Guid SingleListBoardId = Guid.Parse("30000000-0000-0000-0000-000000000001");
  private static readonly Guid ScrumBoardId = Guid.Parse("30000000-0000-0000-0000-000000000002");
  private static readonly Guid SingleListId = Guid.Parse("40000000-0000-0000-0000-000000000001");
  private static readonly Guid BacklogListId = Guid.Parse("40000000-0000-0000-0000-000000000002");
  private static readonly Guid SprintListId = Guid.Parse("40000000-0000-0000-0000-000000000003");
  private static readonly Guid ReviewListId = Guid.Parse("40000000-0000-0000-0000-000000000004");
  private static readonly Guid DoneListId = Guid.Parse("40000000-0000-0000-0000-000000000005");
  private static readonly Guid CompletedReviewCardId = Guid.Parse("50000000-0000-0000-0000-000000000001");
  private static readonly Guid OpenDoneCardId = Guid.Parse("50000000-0000-0000-0000-000000000002");

  [Fact]
  public async Task Migration_AssignsStatusesAndAlignsCompletion()
  {
    await using var database = await TestDatabaseServer.CreateDatabaseAtMigrationAsync("RemoveLegacyTasks");

    await database.InsertWorkspaceWithProjectsAsync(WorkspaceId, ProjectId, ScrumProjectId);
    await database.ExecuteAsync($"""
      INSERT INTO boards (id, workspace_id, project_id, name, template, created_at, updated_at)
      VALUES
        ('{SingleListBoardId}', '{WorkspaceId}', '{ProjectId}', 'Single', 'CUSTOM', now(), now()),
        ('{ScrumBoardId}', '{WorkspaceId}', '{ScrumProjectId}', 'Scrum', 'AGILE_SCRUM', now(), now());
      INSERT INTO board_lists (id, board_id, workspace_id, project_id, title, position, created_at, updated_at)
      VALUES
        ('{SingleListId}', '{SingleListBoardId}', '{WorkspaceId}', '{ProjectId}', 'Only', 0, now(), now()),
        ('{BacklogListId}', '{ScrumBoardId}', '{WorkspaceId}', '{ScrumProjectId}', 'Backlog', 0, now(), now()),
        ('{SprintListId}', '{ScrumBoardId}', '{WorkspaceId}', '{ScrumProjectId}', 'Sprint', 5, now(), now()),
        ('{ReviewListId}', '{ScrumBoardId}', '{WorkspaceId}', '{ScrumProjectId}', 'Review', 7, now(), now()),
        ('{DoneListId}', '{ScrumBoardId}', '{WorkspaceId}', '{ScrumProjectId}', 'Done', 9, now(), now());
      INSERT INTO board_cards (id, board_id, list_id, workspace_id, project_id, title, labels, is_completed, completed_at, position, created_at, updated_at)
      VALUES
        ('{CompletedReviewCardId}', '{ScrumBoardId}', '{ReviewListId}', '{WorkspaceId}', '{ScrumProjectId}', 'Checked in review', ARRAY[]::text[], TRUE, now(), 0, now(), now()),
        ('{OpenDoneCardId}', '{ScrumBoardId}', '{DoneListId}', '{WorkspaceId}', '{ScrumProjectId}', 'Open in done', ARRAY[]::text[], FALSE, NULL, 0, now(), now());
      """);

    await using (var dbContext = database.CreateDbContext())
    {
      await dbContext.Database.MigrateAsync();
    }

    Assert.Equal("TODO", await ListStatusAsync(database, SingleListId));
    Assert.Equal("TODO", await ListStatusAsync(database, BacklogListId));
    Assert.Equal("TODO", await ListStatusAsync(database, SprintListId));
    Assert.Equal("IN_PROGRESS", await ListStatusAsync(database, ReviewListId));
    Assert.Equal("DONE", await ListStatusAsync(database, DoneListId));
    Assert.False(await database.ScalarAsync<bool>(
      $"SELECT is_completed FROM board_cards WHERE id = '{CompletedReviewCardId}'"));
    Assert.True(await database.ScalarAsync<bool>(
      $"SELECT is_completed AND completed_at IS NOT NULL FROM board_cards WHERE id = '{OpenDoneCardId}'"));
  }

  private static Task<string?> ListStatusAsync(TestDatabase database, Guid listId) =>
    database.ScalarAsync<string>(
      $"SELECT task_status FROM board_lists WHERE id = '{listId}'");
}
