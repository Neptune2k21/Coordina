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

    Assert.Equal(new PlanningPreferences(0, null, true, true), preferences);
  }

  [Fact]
  public async Task Save_IsReturnedByTheNextGet_AndBelongsToItsUser()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var saved = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { availableMinutes = 120, context = " #deep work ", preferAssigned = false, includeUnassigned = false });
    var read = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Get, PreferencesPath);

    Assert.Equal(new PlanningPreferences(120, "deep-work", false, false), saved);
    Assert.Equal(saved, read);
  }

  [Fact]
  public async Task Save_LeavingFieldsOut_ResetsThemToTheDefaults()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { availableMinutes = 60, preferAssigned = false });

    var saved = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { });

    Assert.Equal(new PlanningPreferences(0, null, true, true), saved);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(100000)]
  public async Task Save_OutOfRangeMinutes_ReturnsValidationProblem(int availableMinutes)
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");

    var response = await scope.SendAsync(HttpMethod.Put, PreferencesPath, new { availableMinutes });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Save_DoesNotChangeAnotherMembersPreferences()
  {
    var scope = await BoardScope.CreateAsync(Api, "BASIC");
    var member = await scope.AddMemberAsync();

    await scope.SendForAsync<PlanningPreferences>(HttpMethod.Put, PreferencesPath, new { availableMinutes = 240 });
    var membersView = await scope.SendForAsync<PlanningPreferences>(HttpMethod.Get, PreferencesPath, accessToken: member.AccessToken);

    Assert.Equal(0, membersView.AvailableMinutes);
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
    int AvailableMinutes,
    string? Context,
    bool PreferAssigned,
    bool IncludeUnassigned);
}
