using Coordina.Quality.Checks;
using Coordina.Quality.Rules.CSharp;
using Coordina.Quality.Rules.Files;
using Coordina.Quality.Tests.Support;

namespace Coordina.Quality.Tests.Rules;

public sealed class StructureRulesTests
{
  private static readonly string[] ProjectFiles =
  [
    "src/api/Coordina.Api.csproj",
    "tests/api/Coordina.Api.Tests.csproj",
    "tests/api-postgres/Coordina.Api.Postgres.Tests.csproj"
  ];

  [Fact]
  public void FileName_MatchesOneOfItsTypes()
  {
    var rule = new FileMatchesTypeRule();

    Assert.Empty(RuleHarness.Check(rule, "src/api/core/Result.cs", "public class Result<T> { } public class Error { }"));
    Assert.Empty(RuleHarness.Check(rule, "src/api/Program.cs", "var app = 1;"));
    Assert.Equal(
      ["Name this file after one of its types: Board, Card."],
      RuleHarness.Messages(rule, "src/api/core/Things.cs", "namespace X;\npublic class Board { }\npublic class Card { }"));
  }

  [Theory]
  [InlineData("PostgresBoardStore.Cards.cs", "public sealed partial class PostgresBoardStore { }", true)]
  [InlineData("PostgresBoardStore.Cards.cs", "public sealed class PostgresBoardStore { }", false)]
  [InlineData("PostgresBoardStore.Cards.Extra.cs", "public sealed partial class PostgresBoardStore { }", false)]
  [InlineData("OtherStore.Cards.cs", "public sealed partial class PostgresBoardStore { }", false)]
  public void FileMatchesType_LetsAPartialTypeNameItsParts(string fileName, string source, bool allowed)
  {
    var violations = RuleHarness.Check(new FileMatchesTypeRule(), $"src/api/modules/boards/Infrastructure/{fileName}", $"namespace X;\n{source}");

    Assert.Equal(allowed, violations.Length == 0);
  }

  [Theory]
  [InlineData("src/api/modules/boards/Domain", "Coordina.Api.Modules.Boards.Domain")]
  [InlineData("src/api", "Coordina.Api")]
  [InlineData("tests/api/Integration/Boards", "Coordina.Api.Tests.Integration.Boards")]
  [InlineData("tests/api-postgres/Support", "Coordina.Api.Postgres.Tests.Support")]
  public void Namespace_IsTheProjectNameFollowedByFolders(string directory, string expected)
  {
    Assert.Equal(expected, new NamespaceMatchesFolderRule(ProjectFiles).ExpectedNamespace(directory));
  }

  [Fact]
  public void Namespace_FlagsMismatchesAndExemptsProgram()
  {
    var rule = new NamespaceMatchesFolderRule(ProjectFiles);

    Assert.Equal(
      ["Change namespace 'Coordina.Api.Tests' to 'Coordina.Api.Tests.Integration.Boards', which matches the folder."],
      RuleHarness.Messages(rule, "tests/api/Integration/Boards/BoardTests.cs", "namespace Coordina.Api.Tests;\npublic class BoardTests { }"));
    Assert.Empty(RuleHarness.Check(rule, "src/api/modules/boards/Domain/Board.cs", "namespace Coordina.Api.Modules.Boards.Domain;\npublic class Board { }"));
    Assert.Empty(RuleHarness.Check(rule, "src/api/Program.cs", "var app = 1;\npublic partial class Program;"));
  }

  [Theory]
  [InlineData("src/api/modules/boards/BoardsModule.cs", 0)]
  [InlineData("src/api/modules/boards/BoardsEndpoints.cs", 0)]
  [InlineData("src/api/modules/boards/Application/BoardRules.cs", 0)]
  [InlineData("src/api/modules/boards/BoardEndpoints.cs", 1)]
  [InlineData("src/api/modules/auth/AuthOptions.cs", 1)]
  [InlineData("src/api/modules/boards/Helpers/BoardHelper.cs", 1)]
  [InlineData("src/api/core/Results/Result.cs", 0)]
  public void ModuleLayout_KeepsTheDocumentedShape(string path, int expected)
  {
    Assert.Equal(expected, RuleHarness.Check(new ModuleLayoutRule(), path, "public class Sample { }").Length);
  }

  [Theory]
  [InlineData("src/api/modules/boards/Application/BoardEntity.cs", "BoardEntity", "Move EF entity 'BoardEntity' to Infrastructure.")]
  [InlineData("src/api/modules/boards/Domain/CreateBoardRequest.cs", "CreateBoardRequest", "Move HTTP contract 'CreateBoardRequest' to Contracts.")]
  [InlineData("src/api/modules/boards/Application/PostgresBoardStore.cs", "PostgresBoardStore", "Move store implementation 'PostgresBoardStore' to Infrastructure.")]
  [InlineData("src/api/modules/boards/Infrastructure/BoardCatalogService.cs", "BoardCatalogService", "Move service 'BoardCatalogService' to Application.")]
  public void TypePlacement_FollowsTheNameSuffix(string path, string type, string expected)
  {
    Assert.Equal([expected], RuleHarness.Messages(new TypePlacementRule(), path, $"public class {type} {{ }}"));
  }

  [Fact]
  public void Lengths_AreWarningsAboveTheLimits()
  {
    var longMethod = "public class Sample\n{\n  public void Run()\n  {\n"
      + string.Concat(Enumerable.Repeat("    var count = 1;\n", MethodLengthRule.MaxLines))
      + "  }\n}\n";
    var longFile = string.Concat(Enumerable.Repeat("// line\n", FileLengthRule.MaxLines + 1));

    var method = Assert.Single(RuleHarness.Check(new MethodLengthRule(), "src/api/core/Sample.cs", longMethod));
    var file = Assert.Single(RuleHarness.Check(new FileLengthRule(), "src/api/core/Sample.cs", longFile));

    Assert.Equal(Severity.Warning, method.Rule.Severity);
    Assert.Equal(Severity.Warning, file.Rule.Severity);
    Assert.Empty(RuleHarness.Check(new FileLengthRule(), "src/api/core/Sample.cs", "// short\n"));
  }

  [Theory]
  [InlineData("src/web/src/features/board/board-api.ts", 0)]
  [InlineData("src/web/src/features/board/board-api.test.ts", 0)]
  [InlineData("src/web/e2e/board-drag.spec.ts", 0)]
  [InlineData("src/web/src/App.tsx", 0)]
  [InlineData("src/web/src/App.test.tsx", 0)]
  [InlineData("src/web/src/features/board/BoardApi.ts", 1)]
  [InlineData("src/web/src/features/myBoard/board-api.ts", 1)]
  [InlineData("src/web/src/setupTests.ts", 1)]
  public void WebFiles_UseKebabCase(string path, int expected)
  {
    Assert.Equal(expected, RuleHarness.Check(new WebFileNamingRule(), path, string.Empty).Length);
  }

  [Theory]
  [InlineData("src/graph-engine/src/core/graph_types.cpp", 0)]
  [InlineData("tests/graph-engine/graph_engine_tests.cpp", 0)]
  [InlineData("src/graph-engine/src/core/GraphTypes.cpp", 1)]
  [InlineData("src/graph-engine/src/criticalPath/path.h", 1)]
  public void GraphEngineFiles_UseSnakeCase(string path, int expected)
  {
    Assert.Equal(expected, RuleHarness.Check(new GraphEngineFileNamingRule(), path, string.Empty).Length);
  }

  [Theory]
  [InlineData("  // NOLINTNEXTLINE(readability-suspicious-call-argument): the edge is reversed on purpose.")]
  [InlineData("int x = 0; // NOLINT(misc-const-correctness): written by the C caller.")]
  [InlineData("// NOLINTBEGIN(cppcoreguidelines-pro-type-vararg): printf writes the report.")]
  [InlineData("// NOLINTEND(cppcoreguidelines-pro-type-vararg)")]
  [InlineData("// Lint comments are written NOLINT(check): reason.")]
  public void Nolint_WithChecksAndAReason_IsAllowed(string line)
  {
    Assert.Empty(RuleHarness.Check(new NolintReasonRule(), "src/graph-engine/src/core/graph_types.cpp", line));
  }

  [Theory]
  [InlineData("int x = 0; // NOLINT", "Name the checks this silences: '// NOLINT(check-name): reason'.")]
  [InlineData("x(); // see NOLINT above", "Name the checks this silences: '// NOLINT(check-name): reason'.")]
  [InlineData("/* NOLINTNEXTLINE() */", "Name the checks this silences: '// NOLINTNEXTLINE(check-name): reason'.")]
  [InlineData("// NOLINTNEXTLINE(bugprone-sizeof-expression)", "Say why after the checks: '// NOLINTNEXTLINE(bugprone-sizeof-expression): reason'.")]
  [InlineData("// NOLINTBEGIN(cert-err33-c):  ", "Say why after the checks: '// NOLINTBEGIN(cert-err33-c): reason'.")]
  public void Nolint_WithoutChecksOrReason_IsReported(string line, string expected)
  {
    Assert.Equal(
      [expected],
      RuleHarness.Messages(new NolintReasonRule(), "tests/graph-engine/abi_layout.cpp", line));
  }

  [Fact]
  public void Nolint_OnlyAppliesToTheGraphEngine()
  {
    Assert.Empty(RuleHarness.Check(new NolintReasonRule(), "src/web/src/lib/utils.ts", "// NOLINT"));
  }
}
