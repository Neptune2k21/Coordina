using System.Diagnostics;
using Coordina.Api.Postgres.Tests.Support;
using Xunit.Abstractions;

namespace Coordina.Api.Postgres.Tests.Integration.Work;

/// <summary>
/// Measures "My work" on a workspace of 10 boards with 200 cards each, on the
/// real store and graph engine. The bound is loose on purpose: it catches a
/// regression by an order of magnitude, and the measured time is printed.
/// </summary>
public sealed class MyWorkPerformanceTests(PostgresContainerFixture postgres, ITestOutputHelper output)
  : PostgresApiTest(postgres)
{
  private const int Boards = 10;
  private const int CardsPerBoard = 200;
  private const int Runs = 5;

  [Fact]
  public async Task MyWork_OnTenBoardsOfTwoHundredCards_AnswersWithinTheBound()
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<Created>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Large Team" });

    for (var index = 0; index < Boards; index++)
    {
      var project = await Api.SendForAsync<Created>(
        HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken, new { name = $"Project {index:D2}" });
      await Api.SendForAsync<Created>(
        HttpMethod.Post,
        $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
        user.AccessToken,
        new { name = "Board", template = "BASIC", includeExampleCards = false });
    }

    await SeedCardsAsync(workspace.Id, user.Id);

    var path = $"/me/work?workspaceId={workspace.Id}";
    var first = await Api.SendForAsync<Work>(HttpMethod.Get, path, user.AccessToken);
    var timings = new List<double>();

    for (var run = 0; run < Runs; run++)
    {
      var stopwatch = Stopwatch.StartNew();
      await Api.SendForAsync<Work>(HttpMethod.Get, path, user.AccessToken);
      timings.Add(stopwatch.Elapsed.TotalMilliseconds);
    }

    timings.Sort();
    output.WriteLine(
      $"My work, {Boards} boards x {CardsPerBoard} cards: median {timings[Runs / 2]:F0} ms, max {timings[^1]:F0} ms over {Runs} runs.");

    Assert.Equal(Boards * CardsPerBoard, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM board_cards WHERE workspace_id = '{workspace.Id}'"));
    Assert.NotEmpty(first.Next);
    Assert.True(timings[Runs / 2] < 2000, $"My work took {timings[Runs / 2]:F0} ms (median).");
  }

  // Every board gets cards spread over its lists. Every third card depends on
  // the one before it, and every fourth is assigned to the user.
  private Task SeedCardsAsync(Guid workspaceId, Guid userId) =>
    Database.ExecuteAsync($"""
      INSERT INTO board_cards (id, board_id, list_id, workspace_id, project_id, title, labels,
                               estimated_minutes, is_completed, completed_at, position, created_at, updated_at)
      SELECT gen_random_uuid(), l.board_id, l.id, l.workspace_id, l.project_id,
             'Card ' || n, ARRAY['load']::text[], 30 + (n % 4) * 30,
             l.task_status = 'DONE', CASE WHEN l.task_status = 'DONE' THEN now() END,
             n, now(), now()
      FROM board_lists l
      CROSS JOIN generate_series(1, {CardsPerBoard}) AS n
      WHERE l.workspace_id = '{workspaceId}'
        AND n % (SELECT count(*) FROM board_lists x WHERE x.board_id = l.board_id) = l.position;

      INSERT INTO board_card_dependencies (id, card_id, depends_on_card_id, created_at)
      SELECT gen_random_uuid(), c.id, p.id, now()
      FROM board_cards c
      JOIN board_cards p ON p.list_id = c.list_id AND p.position = c.position - 1
      WHERE c.workspace_id = '{workspaceId}' AND c.position % 3 = 0;

      INSERT INTO board_card_assignees (id, card_id, user_id, assigned_at)
      SELECT gen_random_uuid(), c.id, '{userId}', now()
      FROM board_cards c
      WHERE c.workspace_id = '{workspaceId}' AND c.position % 4 = 0;
      """);

  private sealed record Created(Guid Id);

  private sealed record Work(IReadOnlyCollection<object> Next);
}
