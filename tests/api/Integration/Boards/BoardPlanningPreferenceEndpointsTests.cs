using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

public sealed class BoardPlanningPreferenceEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private string _workspaceId = string.Empty;
  private string _projectId = string.Empty;

  [Fact]
  public async Task Get_WithNothingSaved_ReturnsTheDefaults()
  {
    var (owner, path) = await CreateBoardAsync();

    var preferences = await Api.SendForAsync<PlanningPreferences>(HttpMethod.Get, path, owner.AccessToken);

    Assert.Equal(new PlanningPreferences(0, null, true, true), preferences);
  }

  [Fact]
  public async Task Save_IsReturnedByTheNextGet_AndBelongsToItsUser()
  {
    var (owner, path) = await CreateBoardAsync();

    var saved = await Api.SendForAsync<PlanningPreferences>(
      HttpMethod.Put,
      path,
      owner.AccessToken,
      new { availableMinutes = 120, context = " #deep work ", preferAssigned = false, includeUnassigned = false });
    var read = await Api.SendForAsync<PlanningPreferences>(HttpMethod.Get, path, owner.AccessToken);

    Assert.Equal(new PlanningPreferences(120, "deep-work", false, false), saved);
    Assert.Equal(saved, read);
  }

  [Fact]
  public async Task Save_LeavingFieldsOut_ResetsThemToTheDefaults()
  {
    var (owner, path) = await CreateBoardAsync();
    await Api.SendForAsync<PlanningPreferences>(
      HttpMethod.Put, path, owner.AccessToken, new { availableMinutes = 60, preferAssigned = false });

    var saved = await Api.SendForAsync<PlanningPreferences>(HttpMethod.Put, path, owner.AccessToken, new { });

    Assert.Equal(new PlanningPreferences(0, null, true, true), saved);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(100000)]
  public async Task Save_OutOfRangeMinutes_ReturnsValidationProblem(int availableMinutes)
  {
    var (owner, path) = await CreateBoardAsync();

    var response = await Api.SendAsync(HttpMethod.Put, path, owner.AccessToken, new { availableMinutes });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Save_DoesNotChangeAnotherMembersPreferences()
  {
    var (owner, path) = await CreateBoardAsync();
    var member = await Api.AddMemberAsync(owner.AccessToken, _workspaceId);

    await Api.SendForAsync<PlanningPreferences>(HttpMethod.Put, path, owner.AccessToken, new { availableMinutes = 240 });
    var membersView = await Api.SendForAsync<PlanningPreferences>(HttpMethod.Get, path, member.AccessToken);

    Assert.Equal(0, membersView.AvailableMinutes);
  }

  [Fact]
  public async Task Get_ForAnUnknownBoard_ReturnsNotFound()
  {
    var (owner, _) = await CreateBoardAsync();

    var response = await Api.SendAsync(HttpMethod.Get, PathFor(Guid.NewGuid().ToString()), owner.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  private async Task<(AuthResponse Owner, string Path)> CreateBoardAsync()
  {
    var owner = await Api.RegisterAsync("Planner");
    var workspace = await Api.CreateWorkspaceAsync(owner.AccessToken, "Planning Team");
    var project = await Api.CreateProjectAsync(owner.AccessToken, workspace.Id, "Planning");
    var board = await Api.CreateBoardAsync(owner.AccessToken, workspace.Id, project.Id, "BASIC", null, "Planning board");

    _workspaceId = workspace.Id;
    _projectId = project.Id;
    return (owner, PathFor(board.Id));
  }

  private string PathFor(string boardId) =>
    $"/workspaces/{_workspaceId}/projects/{_projectId}/boards/{boardId}/planning-preferences";

  private sealed record PlanningPreferences(
    int AvailableMinutes,
    string? Context,
    bool PreferAssigned,
    bool IncludeUnassigned);
}
