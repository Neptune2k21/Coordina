using System.Net;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Boards;

public sealed class BoardPlanningPreferenceEndpointsTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const string PreferencesPath = "/planning-preferences";


  [Fact]
  public async Task Get_WithNothingSaved_ReturnsTheDefaults()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var preferences = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Get, PreferencesPath);

    Assert.Equal(new PlanningPreferences(null, null, true, true), preferences);
  }

  [Fact]
  public async Task Save_IsReturnedByTheNextGet_AndBelongsToItsUser()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var saved = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { maxSize = "m", context = " #deep work ", preferAssigned = false, includeUnassigned = false });
    var read = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Get, PreferencesPath);

    Assert.Equal(new PlanningPreferences("M", "deep-work", false, false), saved);
    Assert.Equal(saved, read);
  }

  [Fact]
  public async Task Save_LeavingFieldsOut_ResetsThemToTheDefaults()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { maxSize = "S", preferAssigned = false });

    var saved = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { });

    Assert.Equal(new PlanningPreferences(null, null, true, true), saved);
  }

  [Theory]
  [InlineData("HUGE")]
  [InlineData("120")]
  public async Task Save_UnknownSize_ReturnsValidationProblem(string maxSize)
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var response = await scope.SendAsync(HttpMethod.Put, PreferencesPath, new { maxSize });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Save_DoesNotChangeAnotherMembersPreferences()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var member = await scope.AddMemberAsync();

    await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { maxSize = "S" });
    var membersView = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Get, PreferencesPath, accessToken: member.AccessToken);

    Assert.Null(membersView.MaxSize);
  }

  [Fact]
  public async Task Get_ForAnUnknownBoard_ReturnsNotFound()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var response = await Api.SendAsync(
      HttpMethod.Get, $"{scope.ProjectPath}/boards/{Guid.NewGuid()}{PreferencesPath}", scope.Owner.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  private sealed record PlanningPreferences(
    string? MaxSize,
    string? Context,
    bool PreferAssigned,
    bool IncludeUnassigned);
}
