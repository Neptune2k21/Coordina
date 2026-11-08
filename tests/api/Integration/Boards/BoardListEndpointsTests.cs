using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

/// <summary>Reordering, changing the status of and deleting lists.</summary>
public sealed class BoardListEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private BoardScope _scope = null!;

  [Fact]
  public async Task UpdateList_Position_MovesItAndRenumbersTheOthers()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var updated = await PatchListAsync(board, "Closed", new { position = 0 });

    Assert.Equal(["Closed", "Open", "Doing"], TitlesInOrder(updated));
    Assert.Equal([0, 1, 2], updated.Lists.OrderBy(list => list.Position).Select(list => list.Position));
  }

  [Fact]
  public async Task UpdateList_TitleOnly_KeepsStatusAndPosition()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var updated = await PatchListAsync(board, "Doing", new { title = "  Building  " });

    var list = updated.Lists.Single(candidate => candidate.Title == "Building");
    Assert.Equal(("IN_PROGRESS", 1), (list.TaskStatus, list.Position));
  }

  [Fact]
  public async Task UpdateList_StatusToDone_CompletesItsCards()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await CreateCardAsync(board, "Doing", "Ship it");

    var updated = await PatchListAsync(board, "Doing", new { taskStatus = "DONE" });

    var card = CardNamed(updated, "Ship it");
    Assert.True(card.IsCompleted);
    Assert.NotNull(card.CompletedAt);
  }

  [Fact]
  public async Task UpdateList_StatusAwayFromDone_ReopensItsCards()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await AddListAsync("Archive", "DONE");
    board = await CreateCardAsync(board, "Archive", "Old work");
    Assert.True(CardNamed(board, "Old work").IsCompleted);

    var updated = await PatchListAsync(board, "Archive", new { taskStatus = "IN_PROGRESS" });

    var card = CardNamed(updated, "Old work");
    Assert.False(card.IsCompleted);
    Assert.Null(card.CompletedAt);
  }

  [Theory]
  [InlineData("Open", "IN_PROGRESS")]
  [InlineData("Closed", "TODO")]
  public async Task UpdateList_LeavingNoTodoOrDoneList_ReturnsConflict(string title, string status)
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var response = await SendAsync(HttpMethod.Patch, ListPath(board, title), new { taskStatus = status });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal(["TODO", "IN_PROGRESS", "DONE"], (await GetBoardAsync()).Lists.OrderBy(list => list.Position).Select(list => list.TaskStatus));
  }

  [Theory]
  [InlineData(3)]
  [InlineData(-1)]
  public async Task UpdateList_PositionOutOfRange_ReturnsValidationProblem(int position)
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var response = await SendAsync(HttpMethod.Patch, ListPath(board, "Open"), new { position });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task UpdateList_WithNothingToChange_ReturnsValidationProblem()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var response = await SendAsync(HttpMethod.Patch, ListPath(board, "Open"), new { });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task DeleteList_MovesItsCardsInOrderToTheEndOfTheTarget()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await CreateCardAsync(board, "Open", "Already here");
    board = await CreateCardAsync(board, "Doing", "First");
    board = await CreateCardAsync(board, "Doing", "Second");

    var updated = await DeleteListAsync(board, "Doing", ListNamed(board, "Open").Id);

    Assert.Equal(["Open", "Closed"], TitlesInOrder(updated));
    Assert.Equal([0, 1], updated.Lists.OrderBy(list => list.Position).Select(list => list.Position));
    Assert.Equal(
      ["Already here", "First", "Second"],
      ListNamed(updated, "Open").Cards.OrderBy(card => card.Position).Select(card => card.Title));
  }

  [Fact]
  public async Task DeleteList_IntoADoneList_CompletesTheMovedCards()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await CreateCardAsync(board, "Doing", "Wrap up");

    var updated = await DeleteListAsync(board, "Doing", ListNamed(board, "Closed").Id);

    Assert.True(CardNamed(updated, "Wrap up").IsCompleted);
  }

  [Fact]
  public async Task DeleteList_Empty_NeedsNoTarget()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var updated = await DeleteListAsync(board, "Doing", null);

    Assert.Equal(["Open", "Closed"], TitlesInOrder(updated));
  }

  [Fact]
  public async Task DeleteList_WithCards_NeedsAnotherListAsTarget()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await CreateCardAsync(board, "Doing", "Keep me");
    var doing = ListNamed(board, "Doing");

    var withoutTarget = await SendAsync(HttpMethod.Delete, ListPath(board, "Doing"));
    var intoItself = await SendAsync(HttpMethod.Delete, $"{ListPath(board, "Doing")}?moveCardsTo={doing.Id}");
    var intoUnknown = await SendAsync(HttpMethod.Delete, $"{ListPath(board, "Doing")}?moveCardsTo={Guid.NewGuid()}");

    Assert.Equal(
      [HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.NotFound],
      [withoutTarget.StatusCode, intoItself.StatusCode, intoUnknown.StatusCode]);
    Assert.Equal("Keep me", Assert.Single(ListNamed(await GetBoardAsync(), "Doing").Cards).Title);
  }

  [Theory]
  [InlineData("Open")]
  [InlineData("Closed")]
  public async Task DeleteList_LastTodoOrDoneList_ReturnsConflict(string title)
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");

    var response = await SendAsync(HttpMethod.Delete, ListPath(board, title));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal(3, (await GetBoardAsync()).Lists.Count);
  }

  [Fact]
  public async Task DeleteList_ATodoListWhenAnotherRemains_IsAllowed()
  {
    var board = await CreateBoardAsync("Open", "Doing", "Closed");
    board = await AddListAsync("Someday", "TODO");

    var updated = await DeleteListAsync(board, "Open", null);

    Assert.Equal(["Doing", "Closed", "Someday"], TitlesInOrder(updated));
  }

  private async Task<BoardResponse> CreateBoardAsync(params string[] listTitles)
  {
    _scope = await BoardScope.CreateAsync(Api, "CUSTOM", listTitles);
    return _scope.Board;
  }

  private Task<BoardResponse> GetBoardAsync() => _scope.GetBoardAsync();

  private Task<BoardResponse> AddListAsync(string title, string taskStatus) =>
    _scope.SendForAsync<BoardResponse>(HttpMethod.Post, "/lists", new { title, taskStatus });

  private Task<BoardResponse> CreateCardAsync(BoardResponse board, string listTitle, string title) =>
    _scope.SendForAsync<BoardResponse>(HttpMethod.Post, $"{ListPath(board, listTitle)}/cards", new { title });

  private Task<BoardResponse> PatchListAsync(BoardResponse board, string title, object body) =>
    _scope.SendForAsync<BoardResponse>(HttpMethod.Patch, ListPath(board, title), body);

  private Task<BoardResponse> DeleteListAsync(BoardResponse board, string title, string? moveCardsTo) =>
    _scope.SendForAsync<BoardResponse>(
      HttpMethod.Delete,
      moveCardsTo is null ? ListPath(board, title) : $"{ListPath(board, title)}?moveCardsTo={moveCardsTo}");

  private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null) =>
    _scope.SendAsync(method, path, body);

  // Relative to the board, as BoardScope sends it.
  private static string ListPath(BoardResponse board, string title) => $"/lists/{ListNamed(board, title).Id}";

  private static BoardListResponse ListNamed(BoardResponse board, string title) =>
    board.Lists.Single(list => list.Title == title);

  private static BoardCardResponse CardNamed(BoardResponse board, string title) =>
    board.Lists.SelectMany(list => list.Cards).Single(card => card.Title == title);

  private static string[] TitlesInOrder(BoardResponse board) =>
    [.. board.Lists.OrderBy(list => list.Position).Select(list => list.Title)];
}
