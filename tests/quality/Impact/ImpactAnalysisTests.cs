using Coordina.Quality.Impact;

namespace Coordina.Quality.Tests.Impact;

public sealed class ImpactAnalysisTests
{
  [Theory]
  [InlineData("docs/boards.mdx", "")]
  [InlineData("README.md", "")]
  [InlineData("src/web/src/features/board/board-api.ts", "web,e2e")]
  [InlineData("src/api/modules/boards/Application/BoardRules.cs", "api,e2e")]
  [InlineData("tests/api-postgres/Integration/Boards/BoardTests.cs", "api,e2e")]
  [InlineData("tests/support/AuthorizedRequests.cs", "api,e2e")]
  [InlineData("src/graph-engine/src/core/graph_types.cpp", "graph-engine,api,e2e")]
  [InlineData("tools/quality/Coordina.Quality/Program.cs", "quality")]
  [InlineData(".githooks/pre-commit", "quality")]
  [InlineData("contracts/openapi.json", "api,web,e2e")]
  [InlineData("contracts/graph-engine-abi.json", "graph-engine,api")]
  [InlineData("tools/analyzers/Coordina.Analyzers/Rules.cs", "api")]
  [InlineData("Makefile", "quality,graph-engine,api,web,e2e")]
  [InlineData(".github/workflows/quality.yml", "quality,graph-engine,api,web,e2e")]
  [InlineData("src/web/README.md", "web,e2e")]
  [InlineData("src/web/pnpm-lock.yaml", "quality,web,e2e")]
  public void Change_SelectsTheSuitesItCanBreak(string path, string expected)
  {
    var plan = ImpactAnalysis.Analyze([path]);

    Assert.Equal(expected, string.Join(',', plan.Suites.Select(suite => suite.Name)));
  }

  [Fact]
  public void SeveralChanges_CombineSuitesAndKeepEachReason()
  {
    var plan = ImpactAnalysis.Analyze(
    [
      "src/web/src/App.tsx",
      "src/graph-engine/src/core/graph_types.cpp",
      "docs/architecture.mdx"
    ]);

    Assert.Equal("graph-engine,api,web,e2e", string.Join(',', plan.Suites.Select(suite => suite.Name)));
    Assert.Equal(2, plan.Reasons[Suite.EndToEnd].Count);
  }

  [Fact]
  public void ApiChange_ThatAltersTheContract_ChecksTheWebApp()
  {
    var internalOnly = ImpactAnalysis.Analyze(["src/api/modules/boards/Application/BoardRules.cs"]);
    var withContract = ImpactAnalysis.Analyze(
    [
      "src/api/modules/boards/Contracts/BoardResponse.cs",
      "contracts/openapi.json",
      "src/web/src/types/api-contract.ts"
    ]);

    Assert.False(internalOnly.Includes(Suite.Web));
    Assert.True(withContract.Includes(Suite.Web));
    Assert.Contains(
      withContract.Reasons[Suite.Web],
      reason => reason.Contains("contract", StringComparison.Ordinal));
  }
}
