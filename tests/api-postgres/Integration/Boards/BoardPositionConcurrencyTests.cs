using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Boards;

public sealed class BoardPositionConcurrencyTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private const int ConcurrentRequests = 12;

  [Fact]
  public async Task Cards_CreatedTogether_GetDistinctPositions()
  {
    var flow = await CreateFlowAsync("Todo", "Done");
    var listId = flow.ListIds[0];

    await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(index =>
      SendAsync(flow, HttpMethod.Post, $"lists/{listId}/cards", new { title = $"Card {index}" })));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_cards WHERE list_id = '{listId}'");
  }

  [Fact]
  public async Task Lists_CreatedTogether_GetDistinctPositions()
  {
    var flow = await CreateFlowAsync("Todo");

    await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(index =>
      SendAsync(flow, HttpMethod.Post, "lists", new { title = $"List {index}" })));

    await AssertDistinctAsync(
      ConcurrentRequests + 1,
      $"SELECT count(DISTINCT position) FROM board_lists WHERE board_id = '{flow.BoardId}'");
  }

  [Fact]
  public async Task Subtasks_CreatedTogether_GetDistinctPositions()
  {
    var flow = await CreateFlowAsync("Todo");
    var cardId = await CreateCardAsync(flow, flow.ListIds[0], "Parent");

    await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(index =>
      SendAsync(flow, HttpMethod.Post, $"cards/{cardId}/subtasks", new { title = $"Step {index}" })));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_card_subtasks WHERE card_id = '{cardId}'");
  }

  [Fact]
  public async Task Cards_MovedTogether_IntoOneList_GetDistinctPositions()
  {
    var flow = await CreateFlowAsync("Todo", "Doing", "Done");
    var cardIds = new List<Guid>();

    for (var index = 0; index < ConcurrentRequests; index++)
    {
      cardIds.Add(await CreateCardAsync(flow, flow.ListIds[0], $"Card {index}"));
    }

    var target = flow.ListIds[1];

    await Task.WhenAll(cardIds.Select(cardId =>
      SendAsync(flow, HttpMethod.Patch, $"cards/{cardId}/move", new { listId = target })));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_cards WHERE list_id = '{target}'");
  }

  [Fact]
  public async Task CardsSharingAPosition_LoadInCreationOrder()
  {
    var flow = await CreateFlowAsync("Todo", "Done");
    var listId = flow.ListIds[0];
    var created = new List<Guid>();

    for (var index = 0; index < 5; index++)
    {
      created.Add(await CreateCardAsync(flow, listId, $"Card {index}"));
    }

    // What the race used to leave behind. Updating newest first also reverses
    // the physical row order, so only an explicit tie-breaker keeps the order.
    foreach (var cardId in Enumerable.Reverse(created))
    {
      await Database.ExecuteAsync($"UPDATE board_cards SET position = 0 WHERE id = '{cardId}'");
    }

    var first = await LoadCardOrderAsync(flow, listId);
    var second = await LoadCardOrderAsync(flow, listId);

    Assert.Equal(created, first);
    Assert.Equal(first, second);
  }

  private async Task AssertDistinctAsync(int expected, string sql) =>
    Assert.Equal(expected, (int)await Database.ScalarAsync<long>(sql));

  private async Task SendAsync(Flow flow, HttpMethod method, string path, object body)
  {
    using var response = await Api.SendAsync(method, $"{flow.BoardPath}/{path}", flow.AccessToken, body);
    response.EnsureSuccessStatusCode();
  }

  private async Task<Guid> CreateCardAsync(Flow flow, Guid listId, string title)
  {
    var board = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"{flow.BoardPath}/lists/{listId}/cards", flow.AccessToken, new { title });

    return board.Lists.Single(list => list.Id == listId).Cards.Single(card => card.Title == title).Id;
  }

  private async Task<Guid[]> LoadCardOrderAsync(Flow flow, Guid listId)
  {
    var board = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Get, $"/workspaces/{flow.WorkspaceId}/projects/{flow.ProjectId}/board", flow.AccessToken);

    return board.Lists.Single(list => list.Id == listId).Cards.Select(card => card.Id).ToArray();
  }

  private async Task<Flow> CreateFlowAsync(params string[] listTitles)
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Position Team" });
    var project = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken,
      new { name = "Positions" });
    var board = await Api.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      user.AccessToken, new { name = "Positions", template = "CUSTOM", customListTitles = listTitles });

    return new Flow(
      user.AccessToken,
      workspace.Id,
      project.Id,
      board.Id,
      board.Lists.Select(list => list.Id).ToArray());
  }

  private sealed record IdResponse(Guid Id);

  private sealed record CardResponse(Guid Id, string Title);

  private sealed record ListResponse(Guid Id, IReadOnlyCollection<CardResponse> Cards);

  private sealed record BoardResponse(Guid Id, IReadOnlyCollection<ListResponse> Lists);

  private sealed record Flow(
    string AccessToken,
    Guid WorkspaceId,
    Guid ProjectId,
    Guid BoardId,
    Guid[] ListIds)
  {
    public string BoardPath =>
      $"/workspaces/{WorkspaceId}/projects/{ProjectId}/boards/{BoardId}";
  }
}
