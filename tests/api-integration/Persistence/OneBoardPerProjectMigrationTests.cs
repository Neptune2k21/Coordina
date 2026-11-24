using Coordina.Api.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Integration.Tests.Persistence;

/// <summary>OneBoardPerProject keeps the board each project shows and removes the unreachable ones.</summary>
public sealed class OneBoardPerProjectMigrationTests
{
  private static readonly Guid WorkspaceId = Guid.Parse("10000000-0000-0000-0000-000000000011");
  private static readonly Guid ProjectId = Guid.Parse("20000000-0000-0000-0000-000000000011");
  private static readonly Guid OldestBoardId = Guid.Parse("30000000-0000-0000-0000-000000000011");
  private static readonly Guid NewerBoardId = Guid.Parse("30000000-0000-0000-0000-000000000012");
  private static readonly Guid NewerListId = Guid.Parse("40000000-0000-0000-0000-000000000011");
  private static readonly Guid NewerCardId = Guid.Parse("50000000-0000-0000-0000-000000000011");

  [Fact]
  public async Task Migration_KeepsTheOldestBoardOfEachProject()
  {
    await using var database = await TestDatabaseServer.CreateDatabaseAtMigrationAsync("AddCardEstimatedMinutes");

    await database.InsertWorkspaceWithProjectsAsync(WorkspaceId, ProjectId);
    await database.ExecuteAsync($"""
      INSERT INTO boards (id, workspace_id, project_id, name, template, created_at, updated_at)
      VALUES
        ('{OldestBoardId}', '{WorkspaceId}', '{ProjectId}', 'Shown', 'BASIC', now() - interval '1 day', now()),
        ('{NewerBoardId}', '{WorkspaceId}', '{ProjectId}', 'Unreachable', 'BASIC', now(), now());
      INSERT INTO board_lists (id, board_id, workspace_id, project_id, title, position, task_status, created_at, updated_at)
      VALUES ('{NewerListId}', '{NewerBoardId}', '{WorkspaceId}', '{ProjectId}', 'Todo', 0, 'TODO', now(), now());
      INSERT INTO board_cards (id, board_id, list_id, workspace_id, project_id, title, labels, is_completed, position, created_at, updated_at)
      VALUES ('{NewerCardId}', '{NewerBoardId}', '{NewerListId}', '{WorkspaceId}', '{ProjectId}', 'Lost', ARRAY[]::text[], FALSE, 0, now(), now());
      """);

    await using (var dbContext = database.CreateDbContext())
    {
      await dbContext.Database.MigrateAsync();
    }

    Assert.Equal(OldestBoardId, await database.ScalarAsync<Guid>(
      $"SELECT id FROM boards WHERE project_id = '{ProjectId}'"));
    Assert.Equal(1, await database.ScalarAsync<long>(
      $"SELECT count(*) FROM boards WHERE project_id = '{ProjectId}'"));
    Assert.Equal(0, await database.ScalarAsync<long>(
      $"SELECT count(*) FROM board_cards WHERE id = '{NewerCardId}'"));
  }
}
