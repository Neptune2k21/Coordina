using Coordina.Api.Integration.Tests.Support;

namespace Coordina.Api.Integration.Tests.Boards;

public sealed class BoardPositionConcurrencyTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const int ConcurrentRequests = 12;

  [Fact]
  public async Task Cards_CreatedTogether_GetDistinctPositions()
  {
    var scope = await BoardScope.CreateAsync(Api);

    await SendTogetherAsync(index => scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/lists/{scope.FirstList.Id}/cards", new { title = $"Card {index}" }));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_cards WHERE list_id = '{scope.FirstList.Id}'");
  }

  [Fact]
  public async Task Lists_CreatedTogether_GetDistinctPositions()
  {
    var scope = await BoardScope.CreateAsync(Api);

    await SendTogetherAsync(index => scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, "/lists", new { title = $"List {index}" }));

    await AssertDistinctAsync(
      scope.Board.Lists.Count + ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_lists WHERE board_id = '{scope.Board.Id}'");
  }

  [Fact]
  public async Task Subtasks_CreatedTogether_GetDistinctPositions()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Parent");

    await SendTogetherAsync(index => scope.SendForAsync<BoardResponse>(
      HttpMethod.Post, $"/cards/{card.Id}/subtasks", new { title = $"Step {index}" }));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_card_subtasks WHERE card_id = '{card.Id}'");
  }

  [Fact]
  public async Task Cards_MovedTogether_IntoOneList_GetDistinctPositions()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var cards = await AddCardsAsync(scope, ConcurrentRequests);
    var target = scope.Board.Lists.Single(list => list.Position == 1).Id;

    await SendTogetherAsync(index => scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch, $"/cards/{cards[index]}/move", new { listId = target }));

    await AssertDistinctAsync(
      ConcurrentRequests,
      $"SELECT count(DISTINCT position) FROM board_cards WHERE list_id = '{target}'");
  }

  [Fact]
  public async Task CardsSharingAPosition_LoadInCreationOrder()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var created = await AddCardsAsync(scope, 5);

    // What the race used to leave behind. Updating newest first also reverses
    // the physical row order, so only an explicit tie-breaker keeps the order.
    foreach (var cardId in Enumerable.Reverse(created))
    {
      await Database.ExecuteAsync($"UPDATE board_cards SET position = 0 WHERE id = '{cardId}'");
    }

    var first = await scope.GetBoardAsync();
    var second = await scope.GetBoardAsync();

    Assert.Equal(created, CardIds(scope, first));
    Assert.Equal(created, CardIds(scope, second));
  }

  private static Task SendTogetherAsync(Func<int, Task> send) =>
    Task.WhenAll(Enumerable.Range(0, ConcurrentRequests).Select(send));

  private async Task AssertDistinctAsync(int expected, string sql) =>
    Assert.Equal(expected, await Database.ScalarAsync<long>(sql));

  private static async Task<string[]> AddCardsAsync(BoardScope scope, int count)
  {
    var cardIds = new List<string>();

    for (var index = 0; index < count; index++)
    {
      cardIds.Add((await scope.AddCardAsync(scope.FirstList.Id, $"Card {index}")).Id);
    }

    return [.. cardIds];
  }

  private static string[] CardIds(BoardScope scope, BoardResponse board) =>
    [.. board.List(scope.FirstList.Id).Cards.Select(card => card.Id)];
}
