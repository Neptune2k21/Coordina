using System.Text.Json;
using System.Text.Json.Nodes;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Integration.Docs;

/// <summary>
/// The OpenAPI document is the contract between the API and the web app. It
/// is committed, and the web app's types are generated from it, so a change
/// to an endpoint's shape shows up in review and breaks the web build until
/// the web app follows. Run <c>make contract</c> to update it.
/// </summary>
public sealed class ApiContractTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  public const string ContractPath = "contracts/openapi.json";

  private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

  [Fact]
  public async Task Contract_MatchesTheCommittedDocument()
  {
    var generated = await GenerateAsync();
    var path = Path.Combine(SourceTree.RepositoryRoot, ContractPath);

    if (Environment.GetEnvironmentVariable("UPDATE_API_CONTRACT") == "1")
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      await File.WriteAllTextAsync(path, generated);
    }

    Assert.True(
      File.Exists(path) && await File.ReadAllTextAsync(path) == generated,
      $"{ContractPath} is out of date. Run 'make contract' and commit the result.");
  }

  private async Task<string> GenerateAsync()
  {
    var json = await Api.Http.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));
    var document = JsonNode.Parse(json)!.AsObject();

    // The server list depends on the host that served the document.
    document.Remove("servers");

    return document.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
  }
}
