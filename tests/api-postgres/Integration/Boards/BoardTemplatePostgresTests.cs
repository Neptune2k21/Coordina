using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Postgres.Tests.Support;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Postgres.Tests.Integration.Boards;

public sealed class BoardTemplatePostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  public static TheoryData<BoardTemplate> Templates =>
    new(BoardTemplateCatalog.All.Select(template => template.Id));

  [Theory]
  [MemberData(nameof(Templates))]
  public async Task CreateBoard_PersistsExampleContentThroughRealStore(BoardTemplate template)
  {
    var cards = BoardTemplateCatalog.GetLists(template)
      .SelectMany(list => list.Cards)
      .ToArray();
    var flow = await CreateFlowAsync(new { name = "Board", template = EnumCode.Format(template) });

    Assert.Equal(cards.Length, await CountAsync(
      $"SELECT count(*) FROM board_cards WHERE board_id = '{flow.BoardId}'"));
    Assert.Equal(cards.Count(card => card.DueInDays is not null), await CountAsync(
      $"SELECT count(*) FROM board_cards WHERE board_id = '{flow.BoardId}' AND due_date IS NOT NULL"));
    Assert.Equal(cards.Length, await CountAsync(
      $"SELECT count(*) FROM board_cards WHERE board_id = '{flow.BoardId}' AND size IS NOT NULL"));
    Assert.Equal(cards.Sum(card => card.Subtasks.Count), await CountAsync(
      $"""
      SELECT count(*) FROM board_card_subtasks subtask
      JOIN board_cards card ON card.id = subtask.card_id
      WHERE card.board_id = '{flow.BoardId}'
      """));
    Assert.Equal(cards.Sum(card => card.DependsOn.Count), await CountAsync(
      $"""
      SELECT count(*) FROM board_card_dependencies dependency
      JOIN board_cards card ON card.id = dependency.card_id
      JOIN board_cards target ON target.id = dependency.depends_on_card_id
      WHERE card.board_id = '{flow.BoardId}' AND target.board_id = '{flow.BoardId}'
      """));

    var graph = await Api.SendForAsync<GraphResponse>(
      HttpMethod.Get,
      $"{flow.BoardPath}/graph",
      flow.AccessToken);

    Assert.True(graph.IsAcyclic);
    Assert.NotEmpty(graph.NextCards);
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public async Task CreateBoard_ListsOnly_PersistsNoCards(BoardTemplate template)
  {
    var flow = await CreateFlowAsync(new
    {
      name = "Board",
      template = EnumCode.Format(template),
      includeExampleCards = false
    });

    Assert.Equal(BoardTemplateCatalog.GetLists(template).Count, await CountAsync(
      $"SELECT count(*) FROM board_lists WHERE board_id = '{flow.BoardId}'"));
    Assert.Equal(0, await CountAsync(
      $"SELECT count(*) FROM board_cards WHERE board_id = '{flow.BoardId}'"));
  }

  [Fact]
  public async Task CreateBoard_Blank_PersistsAndReloadsWithNoLists()
  {
    var flow = await CreateFlowAsync(new { name = "Board", template = "BLANK" });

    Assert.Equal("BLANK", await Database.ScalarAsync<string>(
      $"SELECT template FROM boards WHERE id = '{flow.BoardId}'"));
    Assert.Equal(0, await CountAsync(
      $"SELECT count(*) FROM board_lists WHERE board_id = '{flow.BoardId}'"));

    var reloaded = await Api.SendForAsync<IdResponse>(
      HttpMethod.Get,
      $"/workspaces/{flow.WorkspaceId}/projects/{flow.ProjectId}/board",
      flow.AccessToken);

    Assert.Equal(flow.BoardId, reloaded.Id);
  }

  private async Task<int> CountAsync(string sql) =>
    (int)await Database.ScalarAsync<long>(sql);

  private async Task<Flow> CreateFlowAsync(object body)
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Template Team" });
    var project = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken,
      new { name = "Templates" });
    var board = await Api.SendForAsync<IdResponse>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      user.AccessToken, body);

    return new Flow(user.AccessToken, workspace.Id, project.Id, board.Id);
  }

  private sealed record IdResponse(Guid Id);

  private sealed record GraphResponse(
    bool IsAcyclic,
    IReadOnlyCollection<IdResponse> NextCards);

  private sealed record Flow(
    string AccessToken,
    Guid WorkspaceId,
    Guid ProjectId,
    Guid BoardId)
  {
    public string BoardPath =>
      $"/workspaces/{WorkspaceId}/projects/{ProjectId}/boards/{BoardId}";
  }
}
