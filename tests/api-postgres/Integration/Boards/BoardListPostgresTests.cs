using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Boards;

/// <summary>Reordering, changing the status of and deleting lists, checked in the database rows.</summary>
public sealed class BoardListPostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  private static readonly string[] ListTitles = ["Open", "Doing", "Closed"];

  [Fact]
  public async Task Reorder_RenumbersEveryListOfTheBoard()
  {
    var board = await CreateBoardAsync();

    await Api.SendForAsync<Board>(
      HttpMethod.Patch, board.ListPath("Closed"), board.AccessToken, new { position = 0 });

    Assert.Equal(
      ["Closed:0", "Open:1", "Doing:2"],
      await RowsAsync($"SELECT title || ':' || position FROM board_lists WHERE board_id = '{board.Id}' ORDER BY position"));
  }

  [Fact]
  public async Task StatusChange_CompletesAndReopensTheListsCards()
  {
    var board = await CreateBoardAsync();
    var cardId = await board.CreateCardAsync(Api, "Doing", "Real store card");

    await Api.SendForAsync<Board>(
      HttpMethod.Patch, board.ListPath("Doing"), board.AccessToken, new { taskStatus = "DONE" });
    Assert.Equal(["true:true"], await CardCompletionAsync(cardId));

    await Api.SendForAsync<Board>(
      HttpMethod.Patch, board.ListPath("Doing"), board.AccessToken, new { taskStatus = "TODO" });
    Assert.Equal(["false:false"], await CardCompletionAsync(cardId));
  }

  [Fact]
  public async Task Delete_MovesTheCardsAndRemovesTheList()
  {
    var board = await CreateBoardAsync();
    var first = await board.CreateCardAsync(Api, "Doing", "First");
    var second = await board.CreateCardAsync(Api, "Doing", "Second");
    var closed = board.ListId("Closed");

    await Api.SendForAsync<Board>(
      HttpMethod.Delete, $"{board.ListPath("Doing")}?moveCardsTo={closed}", board.AccessToken);

    Assert.Equal(
      ["Open:0", "Closed:1"],
      await RowsAsync($"SELECT title || ':' || position FROM board_lists WHERE board_id = '{board.Id}' ORDER BY position"));
    Assert.Equal(
      [$"{first}:{closed}:true", $"{second}:{closed}:true"],
      await RowsAsync($"SELECT id || ':' || list_id || ':' || (is_completed AND completed_at IS NOT NULL) FROM board_cards WHERE id IN ('{first}', '{second}') ORDER BY position"));
  }

  private async Task<string[]> CardCompletionAsync(Guid cardId) =>
    await RowsAsync($"SELECT is_completed || ':' || (completed_at IS NOT NULL) FROM board_cards WHERE id = '{cardId}'");

  private async Task<string[]> RowsAsync(string sql)
  {
    await using var connection = await Database.OpenConnectionAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<string>();

    while (await reader.ReadAsync())
    {
      rows.Add(reader.GetString(0));
    }

    return [.. rows];
  }

  private async Task<BoardScope> CreateBoardAsync()
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<Created>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Lists Team" });
    var project = await Api.SendForAsync<Created>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken, new { name = "Lists" });
    var board = await Api.SendForAsync<Board>(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      user.AccessToken,
      new { name = "Lists", template = "CUSTOM", customListTitles = ListTitles });

    return new BoardScope(user.AccessToken, $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}", board);
  }

  private sealed record Created(Guid Id);

  private sealed record Board(Guid Id, IReadOnlyCollection<List> Lists);

  private sealed record List(Guid Id, string Title, IReadOnlyCollection<Card> Cards);

  private sealed record Card(Guid Id, string Title);

  private sealed record BoardScope(string AccessToken, string Path, Board Board)
  {
    public Guid Id => Board.Id;

    public Guid ListId(string title) => Board.Lists.Single(list => list.Title == title).Id;

    public string ListPath(string title) => $"{Path}/lists/{ListId(title)}";

    public async Task<Guid> CreateCardAsync(ApiClient api, string listTitle, string title)
    {
      var board = await api.SendForAsync<Board>(
        HttpMethod.Post, $"{ListPath(listTitle)}/cards", AccessToken, new { title });

      return board.Lists.SelectMany(list => list.Cards).Single(card => card.Title == title).Id;
    }
  }
}
