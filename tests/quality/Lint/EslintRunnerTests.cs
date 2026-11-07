using Coordina.Quality.Checks;
using Coordina.Quality.Lint;

namespace Coordina.Quality.Tests.Lint;

public sealed class EslintRunnerTests
{
  [Fact]
  public void Parse_TurnsFindingsIntoViolations()
  {
    var violations = new EslintRunner("/repo").Parse(
      """
      [
        {
          "filePath": "/repo/src/web/src/lib/utils.ts",
          "messages": [
            { "ruleId": "eqeqeq", "severity": 2, "line": 9, "message": "Expected '==='." },
            { "ruleId": "no-console", "severity": 1, "line": 10, "message": "Unexpected console." },
            { "ruleId": null, "severity": 1, "line": 1, "message": "File ignored." },
            { "ruleId": null, "fatal": true, "severity": 2, "line": 3, "message": "Parsing error." }
          ]
        },
        { "filePath": "/repo/src/web/src/clean.ts", "messages": [] }
      ]
      """,
      "/repo");

    Assert.Equal(
      [
        ("eslint/eqeqeq", Severity.Error, 9),
        ("eslint/no-console", Severity.Warning, 10),
        ("eslint/parse-error", Severity.Error, 3)
      ],
      violations.Select(violation => (violation.Rule.Id, violation.Rule.Severity, violation.Line)));
    Assert.All(violations, violation => Assert.Equal("src/web/src/lib/utils.ts", violation.Path));
  }

  [Theory]
  [InlineData("src/web/src/lib/utils.ts", true)]
  [InlineData("src/web/src/App.tsx", true)]
  [InlineData("src/web/e2e/board-drag.spec.ts", true)]
  [InlineData("src/web/eslint.config.js", true)]
  [InlineData("src/web/src/types/api-contract.ts", false)]
  [InlineData("src/web/src/index.css", false)]
  [InlineData("src/api/Program.cs", false)]
  public void IsLinted_CoversTheWebAppsScripts(string path, bool expected)
  {
    Assert.Equal(expected, EslintRunner.IsLinted(path));
  }

  [Fact]
  public void Run_WithNothingToLint_DoesNotStartEslint()
  {
    var runner = new EslintRunner("/path/that/does/not/exist");

    Assert.False(runner.AppliesHere);
    Assert.Empty(runner.Run(["src/api/Program.cs", "docs/boards.mdx"]));
  }
}
