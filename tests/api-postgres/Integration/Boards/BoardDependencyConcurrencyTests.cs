using System.Net;
using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Boards;

public sealed class BoardDependencyConcurrencyTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private const int Rounds = 10;
  private const int ConcurrentRequests = 8;

  [Fact]
  public async Task OppositeDependencies_SentTogether_NeverCreateACycle()
  {
    var flow = await CreateFlowAsync(Rounds * 2);

    for (var round = 0; round < Rounds; round++)
    {
      var first = flow.CardIds[round * 2];
      var second = flow.CardIds[(round * 2) + 1];

      var statuses = await Task.WhenAll(
        AddDependencyAsync(flow, first, second),
        AddDependencyAsync(flow, second, first));

      Assert.Equal(
        [HttpStatusCode.OK, HttpStatusCode.Conflict],
        statuses.Order());
      Assert.Equal(1, await CountEdgesBetweenAsync(first, second));
    }

    Assert.True((await GetGraphAsync(flow)).IsAcyclic);
  }

  [Fact]
  public async Task DependenciesClosingALoop_SentTogether_NeverCreateACycle()
  {
    var flow = await CreateFlowAsync(Rounds * 3);

    for (var round = 0; round < Rounds; round++)
    {
      var first = flow.CardIds[round * 3];
      var second = flow.CardIds[(round * 3) + 1];
      var third = flow.CardIds[(round * 3) + 2];

      var statuses = await Task.WhenAll(
        AddDependencyAsync(flow, first, second),
        AddDependencyAsync(flow, second, third),
        AddDependencyAsync(flow, third, first));

      Assert.Equal(2, statuses.Count(status => status == HttpStatusCode.OK));
      Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Conflict));
    }

    Assert.True((await GetGraphAsync(flow)).IsAcyclic);
  }

  [Fact]
  public async Task SameDependency_SentTogether_SucceedsAndStoresOneRow()
  {
    var flow = await CreateFlowAsync(2);

    var statuses = await Task.WhenAll(
      Enumerable.Range(0, ConcurrentRequests)
        .Select(_ => AddDependencyAsync(flow, flow.CardIds[0], flow.CardIds[1])));

    Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    Assert.Equal(1, await CountEdgesBetweenAsync(flow.CardIds[0], flow.CardIds[1]));
  }

  [Fact]
  public async Task Dependency_WhenItWouldCloseACycle_ReturnsConflictSequentially()
  {
    var flow = await CreateFlowAsync(2);

    Assert.Equal(
      HttpStatusCode.OK,
      await AddDependencyAsync(flow, flow.CardIds[0], flow.CardIds[1]));
    Assert.Equal(
      HttpStatusCode.Conflict,
      await AddDependencyAsync(flow, flow.CardIds[1], flow.CardIds[0]));
  }

  private async Task<HttpStatusCode> AddDependencyAsync(
    Flow flow,
    Guid cardId,
    Guid dependsOnCardId)
  {
    using var response = await Api.SendAsync(
      HttpMethod.Post,
      $"{flow.BoardPath}/cards/{cardId}/dependencies",
      flow.AccessToken,
      new { dependsOnCardId });

    return response.StatusCode;
  }

  private Task<GraphResponse> GetGraphAsync(Flow flow) =>
    Api.SendForAsync<GraphResponse>(
      HttpMethod.Get,
      $"{flow.BoardPath}/graph",
      flow.AccessToken);

  private async Task<int> CountEdgesBetweenAsync(Guid first, Guid second) =>
    (int)await Database.ScalarAsync<long>(
      $"""
      SELECT count(*) FROM board_card_dependencies
      WHERE (card_id = '{first}' AND depends_on_card_id = '{second}')
        OR (card_id = '{second}' AND depends_on_card_id = '{first}')
      """);

  private async Task<Flow> CreateFlowAsync(int cardCount)
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Race Team" });
    var project = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken,
      new { name = "Race" });
    var board = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      user.AccessToken, new { name = "Race", template = "BLANK" });
    var boardPath = $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}";
    var withList = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"{boardPath}/lists", user.AccessToken, new { title = "Todo" });
    var listId = withList.Lists.Single().Id;
    var cards = withList;

    for (var index = 0; index < cardCount; index++)
    {
      cards = await Api.SendForAsync<BoardResponse>(
        HttpMethod.Post, $"{boardPath}/lists/{listId}/cards", user.AccessToken,
        new { title = $"Card {index}" });
    }

    return new Flow(
      user.AccessToken,
      boardPath,
      cards.Lists.Single().Cards.Select(card => card.Id).ToArray());
  }

  private sealed record IdResponse(Guid Id);

  private sealed record GraphResponse(bool IsAcyclic);

  private sealed record BoardResponse(Guid Id, IReadOnlyCollection<ListResponse> Lists);

  private sealed record ListResponse(Guid Id, IReadOnlyCollection<IdResponse> Cards);

  private sealed record Flow(string AccessToken, string BoardPath, Guid[] CardIds);
}
