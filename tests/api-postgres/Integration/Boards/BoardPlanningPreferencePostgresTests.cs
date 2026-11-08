using Coordina.Api.Postgres.Tests.Support;

namespace Coordina.Api.Postgres.Tests.Integration.Boards;

public sealed class BoardPlanningPreferencePostgresTests(PostgresContainerFixture postgres)
  : PostgresApiTest(postgres)
{
  [Fact]
  public async Task Save_TwiceKeepsOneRowWithTheLatestValues()
  {
    var user = await Api.RegisterAsync();
    var workspace = await Api.SendForAsync<Created>(
      HttpMethod.Post, "/workspaces", user.AccessToken, new { name = "Planning Team" });
    var project = await Api.SendForAsync<Created>(
      HttpMethod.Post, $"/workspaces/{workspace.Id}/projects", user.AccessToken, new { name = "Planning" });
    var board = await Api.SendForAsync<Created>(
      HttpMethod.Post,
      $"/workspaces/{workspace.Id}/projects/{project.Id}/boards",
      user.AccessToken,
      new { name = "Planning", template = "BASIC" });
    var path = $"/workspaces/{workspace.Id}/projects/{project.Id}/boards/{board.Id}/planning-preferences";

    await Api.SendForAsync<Preferences>(HttpMethod.Put, path, user.AccessToken, new { availableMinutes = 60, context = "ops" });
    await Api.SendForAsync<Preferences>(HttpMethod.Put, path, user.AccessToken, new { availableMinutes = 240, preferAssigned = false });
    var read = await Api.SendForAsync<Preferences>(HttpMethod.Get, path, user.AccessToken);

    Assert.Equal(new Preferences(240, null, false, true), read);
    Assert.Equal(1, await Database.ScalarAsync<long>(
      $"SELECT count(*) FROM board_planning_preferences WHERE board_id = '{board.Id}' AND user_id = '{user.Id}'"));
  }

  private sealed record Created(Guid Id);

  private sealed record Preferences(int AvailableMinutes, string? Context, bool PreferAssigned, bool IncludeUnassigned);
}
