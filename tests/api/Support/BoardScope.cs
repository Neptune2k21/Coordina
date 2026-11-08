namespace Coordina.Api.Tests.Support;

/// <summary>
/// A user who owns a workspace with a project and its board, and the board
/// requests tests make. A test creates one in a line and then reads as its
/// scenario; requests go to <see cref="BoardPath"/> as the owner unless a
/// token is given.
/// </summary>
public sealed class BoardScope
{
  private readonly ApiTestClient _api;

  private BoardScope(
    ApiTestClient api,
    AuthResponse owner,
    WorkspaceResponse workspace,
    ProjectResponse project,
    BoardResponse board)
  {
    _api = api;
    Owner = owner;
    Workspace = workspace;
    Project = project;
    Board = board;
  }

  public AuthResponse Owner { get; }

  public WorkspaceResponse Workspace { get; }

  public ProjectResponse Project { get; }

  /// <summary>The board as it was created.</summary>
  public BoardResponse Board { get; }

  public string ProjectPath => $"/workspaces/{Workspace.Id}/projects/{Project.Id}";

  public string BoardPath => $"{ProjectPath}/boards/{Board.Id}";

  /// <summary>The first list of the board as it was created.</summary>
  public BoardListResponse FirstList => Board.Lists.OrderBy(list => list.Position).First();

  /// <summary>Creates the owner, workspace, project and board, with the default lists for CUSTOM.</summary>
  public static async Task<BoardScope> CreateAsync(
    ApiTestClient api,
    string template = "CUSTOM",
    string[]? customListTitles = null)
  {
    ArgumentNullException.ThrowIfNull(api);

    var owner = await api.RegisterAsync("Board Owner");
    var workspace = await api.CreateWorkspaceAsync(owner.AccessToken, "Board Team");
    var project = await api.CreateProjectAsync(owner.AccessToken, workspace.Id, "Board");
    var board = await api.CreateBoardAsync(owner.AccessToken, workspace.Id, project.Id, template, customListTitles);

    return new BoardScope(api, owner, workspace, project, board);
  }

  public Task<AuthResponse> AddMemberAsync() => _api.AddMemberAsync(Owner.AccessToken, Workspace.Id);

  public Task<BoardResponse> GetBoardAsync(string? accessToken = null) =>
    _api.GetDefaultBoardAsync(accessToken ?? Owner.AccessToken, Workspace.Id, Project.Id);

  /// <summary>Sends a request to a path below the board, such as <c>/cards/{id}</c>.</summary>
  public Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string boardRelativePath,
    object? body = null,
    string? accessToken = null) =>
    _api.SendAsync(method, BoardPath + boardRelativePath, accessToken ?? Owner.AccessToken, body);

  /// <summary>Like <see cref="SendAsync"/>, for a request that must succeed.</summary>
  public Task<T> SendForAsync<T>(
    HttpMethod method,
    string boardRelativePath,
    object? body = null,
    string? accessToken = null) =>
    _api.SendForAsync<T>(method, BoardPath + boardRelativePath, accessToken ?? Owner.AccessToken, body);

  /// <summary>Creates a card from <paramref name="card"/>, a request body, and returns it.</summary>
  public async Task<BoardCardResponse> AddCardAsync(string listId, object card)
  {
    var board = await SendForAsync<BoardResponse>(HttpMethod.Post, $"/lists/{listId}/cards", card);

    return board.Lists.Single(list => list.Id == listId).Cards
      .OrderBy(created => created.Position)
      .Last();
  }

  public Task<BoardCardResponse> AddCardAsync(string listId, string title) =>
    AddCardAsync(listId, new { title });

  public Task<BoardResponse> UpdateCardAsync(string cardId, object changes) =>
    SendForAsync<BoardResponse>(HttpMethod.Patch, $"/cards/{cardId}", changes);

  public Task<BoardResponse> AddDependencyAsync(string cardId, string dependsOnCardId) =>
    SendForAsync<BoardResponse>(HttpMethod.Post, $"/cards/{cardId}/dependencies", new { dependsOnCardId });
}
