using System.Net;
using Coordina.Api.Integration.Tests.Support;

namespace Coordina.Api.Integration.Tests.Boards;

public sealed class BoardDependencyConcurrencyTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const int Rounds = 10;
  private const int ConcurrentRequests = 8;

  [Fact]
  public async Task OppositeDependencies_SentTogether_NeverCreateACycle()
  {
    var scope = await BoardScope.CreateAsync(Api);

    for (var round = 0; round < Rounds; round++)
    {
      var first = await AddCardAsync(scope);
      var second = await AddCardAsync(scope);

      var statuses = await Task.WhenAll(
        AddDependencyAsync(scope, first, second),
        AddDependencyAsync(scope, second, first));

      Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], statuses.Order());
      Assert.Equal(1, await CountEdgesBetweenAsync(first, second));
    }

    Assert.True((await GetGraphAsync(scope)).IsAcyclic);
  }

  [Fact]
  public async Task DependenciesClosingALoop_SentTogether_NeverCreateACycle()
  {
    var scope = await BoardScope.CreateAsync(Api);

    for (var round = 0; round < Rounds; round++)
    {
      var first = await AddCardAsync(scope);
      var second = await AddCardAsync(scope);
      var third = await AddCardAsync(scope);

      var statuses = await Task.WhenAll(
        AddDependencyAsync(scope, first, second),
        AddDependencyAsync(scope, second, third),
        AddDependencyAsync(scope, third, first));

      Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Conflict], statuses.Order());
    }

    Assert.True((await GetGraphAsync(scope)).IsAcyclic);
  }

  [Fact]
  public async Task SameDependency_SentTogether_SucceedsAndStoresOneRow()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var first = await AddCardAsync(scope);
    var second = await AddCardAsync(scope);

    var statuses = await Task.WhenAll(
      Enumerable.Range(0, ConcurrentRequests)
        .Select(_ => AddDependencyAsync(scope, first, second)));

    Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    Assert.Equal(1, await CountEdgesBetweenAsync(first, second));
  }

  private static async Task<string> AddCardAsync(BoardScope scope) =>
    (await scope.AddCardAsync(scope.FirstList.Id, "Card")).Id;

  private static async Task<HttpStatusCode> AddDependencyAsync(
    BoardScope scope,
    string cardId,
    string dependsOnCardId)
  {
    using var response = await scope.SendAsync(
      HttpMethod.Post, $"/cards/{cardId}/dependencies", new { dependsOnCardId });

    return response.StatusCode;
  }

  private static Task<BoardGraphResponse> GetGraphAsync(BoardScope scope) =>
    scope.SendForAsync<BoardGraphResponse>(HttpMethod.Get, "/graph");

  private Task<long> CountEdgesBetweenAsync(string first, string second) =>
    Database.ScalarAsync<long>(
      $"""
      SELECT count(*) FROM board_card_dependencies
      WHERE (card_id = '{first}' AND depends_on_card_id = '{second}')
        OR (card_id = '{second}' AND depends_on_card_id = '{first}')
      """);
}
