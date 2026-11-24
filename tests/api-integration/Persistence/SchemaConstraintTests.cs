using Coordina.Api.Integration.Tests.Support;
using Npgsql;

namespace Coordina.Api.Integration.Tests.Persistence;

/// <summary>What the schema refuses even when the write does not come from the API.</summary>
public sealed class SchemaConstraintTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  [Fact]
  public async Task CardSize_OutsideTheKnownSizes_IsRefused()
  {
    var scope = await BoardScope.CreateAsync(Api);
    var card = await scope.AddCardAsync(scope.FirstList.Id, "Sized");

    var violation = await Assert.ThrowsAsync<PostgresException>(() =>
      Database.ExecuteAsync($"UPDATE board_cards SET size = 'XXXL' WHERE id = '{card.Id}'"));

    Assert.Equal("ck_board_cards_size", violation.ConstraintName);
  }
}
