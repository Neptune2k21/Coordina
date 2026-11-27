using System.Net;
using Coordina.Api.Integration.Tests.Support;

namespace Coordina.Api.Integration.Tests.Boards;

/// <summary>The history the database records, whichever way a card changes.</summary>
public sealed class BoardHistoryTests(ApiTestApplicationFactory factory) : ApiTest(factory)
{
  private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

  [Fact]
  public async Task Events_FollowACardFromItsCreationToItsDeletion()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var (todo, doing, done) = Lists(scope);
    var card = await scope.AddCardAsync(todo.Id, new { title = "Payment API", size = "M" });
    var other = await scope.AddCardAsync(todo.Id, "Design");

    await MoveAsync(scope, card.Id, doing.Id);
    await scope.UpdateCardAsync(card.Id, new { size = "L" });
    await scope.UpdateCardAsync(card.Id, new { assigneeIds = new[] { scope.Owner.User.Id } });
    await scope.AddDependencyAsync(card.Id, other.Id);
    await scope.UpdateCardAsync(card.Id, new { milestoneDate = Today.AddDays(9) });
    await MoveAsync(scope, card.Id, done.Id);
    await scope.SendAsync(HttpMethod.Delete, $"/cards/{card.Id}/dependencies/{other.Id}");
    await scope.SendAsync(HttpMethod.Delete, $"/cards/{card.Id}");

    var events = (await EventsAsync(scope)).Where(entry => entry.CardId == card.Id).ToArray();

    Assert.Equal(
      [
        "CREATED",
        "STATUS_CHANGED",
        "SIZE_CHANGED",
        "ASSIGNEE_ADDED",
        "DEPENDENCY_ADDED",
        "MILESTONE_CHANGED",
        "STATUS_CHANGED",
        "DEPENDENCY_REMOVED",
        "DELETED"
      ],
      events.Select(entry => entry.Kind));
    Assert.Equal((null, "TODO"), (events[0].From, events[0].To));
    Assert.Equal(("TODO", "IN_PROGRESS"), (events[1].From, events[1].To));
    Assert.Equal(("M", "L"), (events[2].From, events[2].To));
    Assert.Equal(scope.Owner.User.Id, events[3].To);
    Assert.Equal(other.Id, events[4].To);
    Assert.Equal(("IN_PROGRESS", "DONE"), (events[6].From, events[6].To));
    Assert.Equal(events.OrderBy(entry => entry.Id), events);
  }

  [Fact]
  public async Task Events_OfAListWhoseStatusChanges_CoverItsCards()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var backlog = await AddBacklogAsync(scope);
    var card = await scope.AddCardAsync(backlog.Id, "Design");

    await scope.SendForAsync<BoardResponse>(
      HttpMethod.Patch,
      $"/lists/{backlog.Id}",
      new { taskStatus = "IN_PROGRESS" });

    var last = (await EventsAsync(scope)).Last(entry => entry.CardId == card.Id);
    Assert.Equal(("STATUS_CHANGED", "TODO", "IN_PROGRESS"), (last.Kind, last.From, last.To));
  }

  [Fact]
  public async Task Events_OfCardsMovedByADeletedList_AreRecorded()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var (_, doing, _) = Lists(scope);
    var backlog = await AddBacklogAsync(scope);
    var card = await scope.AddCardAsync(backlog.Id, "Design");

    var deleted = await scope.SendAsync(HttpMethod.Delete, $"/lists/{backlog.Id}?moveCardsTo={doing.Id}");
    deleted.EnsureSuccessStatusCode();

    var last = (await EventsAsync(scope)).Last(entry => entry.CardId == card.Id);
    Assert.Equal(("STATUS_CHANGED", "IN_PROGRESS"), (last.Kind, last.To));
  }

  [Fact]
  public async Task Events_MovingWithinOneStatus_AreNotRecorded()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var (todo, _, _) = Lists(scope);
    var backlog = await AddBacklogAsync(scope);
    var card = await scope.AddCardAsync(todo.Id, "Design");

    await MoveAsync(scope, card.Id, backlog.Id);

    Assert.Equal(["CREATED"], (await EventsAsync(scope))
      .Where(entry => entry.CardId == card.Id)
      .Select(entry => entry.Kind));
  }

  [Fact]
  public async Task Events_ComeInPages()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var (todo, _, _) = Lists(scope);

    for (var index = 0; index < 5; index++)
    {
      await scope.AddCardAsync(todo.Id, $"Card {index}");
    }

    var all = await EventsAsync(scope);
    var first = await PageAsync(scope, "?limit=3");
    var second = await PageAsync(scope, $"?limit=3&after={first.NextAfter}");
    var refused = await scope.SendAsync(HttpMethod.Get, "/events?limit=100000");

    Assert.Equal(3, first.Events.Count);
    Assert.NotNull(first.NextAfter);
    Assert.Equal(all.Take(6), first.Events.Concat(second.Events));
    Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
  }

  [Fact]
  public async Task Events_OfAnotherWorkspace_AreNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var outsider = await Api.RegisterAsync("Outsider");

    var response = await scope.SendAsync(HttpMethod.Get, "/events", null, outsider.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task DeletingAProject_TakesItsHistoryWithIt()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    await scope.AddCardAsync(scope.FirstList.Id, "Design");

    await Database.ExecuteAsync($"DELETE FROM boards WHERE id = '{scope.Board.Id}'");

    Assert.Equal(0, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM board_card_events WHERE board_id = '{scope.Board.Id}'"));
  }

  private static (BoardListResponse Todo, BoardListResponse Doing, BoardListResponse Done) Lists(BoardScope scope)
  {
    var lists = scope.Board.Lists.ToDictionary(list => list.TaskStatus);

    return (lists["TODO"], lists["IN_PROGRESS"], lists["DONE"]);
  }

  // A second TODO list, since a board keeps at least one.
  private static async Task<BoardListResponse> AddBacklogAsync(BoardScope scope)
  {
    var board = await scope.SendForAsync<BoardResponse>(
      HttpMethod.Post,
      "/lists",
      new { title = "Backlog", taskStatus = "TODO" });

    return board.Lists.Single(list => list.Title == "Backlog");
  }

  private static Task<BoardResponse> MoveAsync(BoardScope scope, string cardId, string listId) =>
    scope.SendForAsync<BoardResponse>(HttpMethod.Patch, $"/cards/{cardId}/move", new { listId });

  private static Task<BoardEventPageResponse> PageAsync(BoardScope scope, string query) =>
    scope.SendForAsync<BoardEventPageResponse>(HttpMethod.Get, $"/events{query}");

  private static async Task<IReadOnlyCollection<BoardCardEventResponse>> EventsAsync(BoardScope scope) =>
    (await PageAsync(scope, "?limit=500")).Events;
}
