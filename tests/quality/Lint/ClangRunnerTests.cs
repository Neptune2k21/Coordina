using Coordina.Quality.Checks;
using Coordina.Quality.Lint;

namespace Coordina.Quality.Tests.Lint;

public sealed class ClangRunnerTests
{
  private const string Root = "/repo";

  [Fact]
  public void ClangFormat_Parse_ReportsEachLineToReformatOnce()
  {
    var violations = ClangFormatRunner.Parse(
      """
      src/graph-engine/src/core/graph_types.cpp:12:4: error: code should be clang-formatted [-Wclang-format-violations]
      int  f( int a ){ return a;}
         ^
      src/graph-engine/src/core/graph_types.cpp:12:8: error: code should be clang-formatted [-Wclang-format-violations]
      int  f( int a ){ return a;}
             ^
      /repo/tests/graph-engine/graph_engine_tests.cpp:3:1: error: code should be clang-formatted [-Wclang-format-violations]
      """,
      Root);

    Assert.Equal(
      [("src/graph-engine/src/core/graph_types.cpp", 12), ("tests/graph-engine/graph_engine_tests.cpp", 3)],
      violations.Select(violation => (violation.Path, violation.Line)));
    Assert.All(violations, violation => Assert.Equal("clang-format", violation.Rule.Id));
  }

  [Fact]
  public void ClangTidy_Parse_NamesTheRuleAfterTheCheck()
  {
    var violations = new ClangTidyRunner(new ClangTools(Root), Root).Parse(
      """
      /repo/src/graph-engine/src/core/graph_snapshot.cpp:55:20: error: use 'contains' to check for membership [readability-container-contains,-warnings-as-errors]
         55 |   if (outgoing_.find(node) != outgoing_.end())
            |                ^
      /repo/src/graph-engine/src/core/graph_snapshot.cpp:55:20: note: this note is not a finding
      /repo/src/graph-engine/src/planning/work_planner.h:19:8: warning: constructor does not initialize these fields: node [cppcoreguidelines-pro-type-member-init,hicpp-member-init]
      src/graph-engine/src/zz/bad.cpp:2:23: error: no member named 'vector' in namespace 'std' [clang-diagnostic-error]
      """,
      Root);

    Assert.Equal(
      [
        ("clang-tidy/readability-container-contains", Severity.Error, "src/graph-engine/src/core/graph_snapshot.cpp", 55),
        ("clang-tidy/cppcoreguidelines-pro-type-member-init", Severity.Warning, "src/graph-engine/src/planning/work_planner.h", 19),
        ("clang-tidy/clang-diagnostic-error", Severity.Error, "src/graph-engine/src/zz/bad.cpp", 2)
      ],
      violations.Select(violation => (violation.Rule.Id, violation.Rule.Severity, violation.Path, violation.Line)));
    Assert.Equal("use 'contains' to check for membership", violations[0].Message);
    Assert.Contains(
      "https://clang.llvm.org/extra/clang-tidy/checks/readability/container-contains.html",
      violations[0].Rule.Rationale,
      StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(".clang-format", true)]
  [InlineData(".clang-tidy", true)]
  [InlineData("tests/graph-engine/.clang-tidy", true)]
  [InlineData("compile_flags.txt", true)]
  [InlineData("tools/clang/requirements.txt", true)]
  [InlineData("src/graph-engine/src/core/graph_types.cpp", false)]
  [InlineData("src/web/.prettierrc", false)]
  public void ClangTools_SettingsThatAffectEveryFile(string path, bool expected)
  {
    Assert.Equal(expected, ClangTools.IsConfiguration(path));
  }

  [Fact]
  public void Runners_WithoutCpp_DoNotStartTheTools()
  {
    var tools = new ClangTools("/path/that/does/not/exist");
    string[] paths = ["src/api/Program.cs", "docs/boards.mdx"];

    Assert.False(tools.AppliesHere);
    Assert.False(tools.IsInstalled);
    Assert.Empty(new ClangFormatRunner(tools, "/path/that/does/not/exist").Run(paths, paths));
    Assert.Empty(new ClangTidyRunner(tools, "/path/that/does/not/exist").Run(paths, paths));
  }
}
