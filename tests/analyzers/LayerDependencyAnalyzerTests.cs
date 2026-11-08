using Coordina.Analyzers.Tests.Support;

namespace Coordina.Analyzers.Tests;

public sealed class LayerDependencyAnalyzerTests
{
  private const string SharedText =
    """
    namespace Coordina.Api.Shared.Text;

    public static class TextNormalizer
    {
      public static string Trimmed(this string value) => value.Trim();

      public static string Normalize(string value) => value;
    }
    """;

  private const string BoardsModule =
    """
    namespace Coordina.Api.Modules.Boards;

    public sealed class Board;
    """;

  [Fact]
  public async Task Core_UsingShared_IsReportedAtTheUsingAndEachUse()
  {
    var diagnostics = await RunAsync(
      ("src/api/core/Time/Clock.cs",
        """
        using Coordina.Api.Shared.Text;

        namespace Coordina.Api.Core.Time;

        public static class Clock
        {
          public static string Name(string value) => TextNormalizer.Normalize(value);

          public static string Short(string value) => value.Trimmed();
        }
        """));

    Assert.Equal(
      ["CQ6001 src/api/core/Time/Clock.cs:1", "CQ6001 src/api/core/Time/Clock.cs:7", "CQ6001 src/api/core/Time/Clock.cs:9"],
      diagnostics);
  }

  [Fact]
  public async Task Core_ReachingSharedThroughAGlobalUsing_IsStillReported()
  {
    var diagnostics = await RunAsync(
      ("src/api/GlobalUsings.cs", "global using Coordina.Api.Shared.Text;"),
      ("src/api/core/Time/Clock.cs",
        """
        namespace Coordina.Api.Core.Time;

        public static class Clock
        {
          public static string Name(string value)
          {
            var normalized = TextNormalizer.Normalize(value);
            return normalized;
          }
        }
        """));

    Assert.Equal(["CQ6001 src/api/core/Time/Clock.cs:7"], diagnostics);
  }

  [Fact]
  public async Task Shared_UsingAModule_IsReported()
  {
    var diagnostics = await RunAsync(
      ("src/api/shared/Text/Labels.cs",
        """
        namespace Coordina.Api.Shared.Text;

        public static class Labels
        {
          public static string Of(Coordina.Api.Modules.Boards.Board board) => board.ToString()!;
        }
        """));

    Assert.Equal(["CQ6001 src/api/shared/Text/Labels.cs:5"], diagnostics);
  }

  [Fact]
  public async Task Modules_UseSharedAndCore_AndMigrationsAreExempt()
  {
    var diagnostics = await RunAsync(
      ("src/api/modules/boards/BoardNames.cs",
        """
        using Coordina.Api.Shared.Text;

        namespace Coordina.Api.Modules.Boards;

        public static class BoardNames
        {
          public static string Of(string value) => TextNormalizer.Normalize(value);
        }
        """),
      ("src/api/core/Persistence/Migrations/Initial.cs",
        """
        namespace Coordina.Api.Core.Persistence.Migrations;

        public sealed class Initial
        {
          public Coordina.Api.Modules.Boards.Board? Board { get; set; }
        }
        """));

    Assert.Empty(diagnostics);
  }

  private static Task<string[]> RunAsync(params (string Path, string Source)[] files) =>
    AnalyzerHarness.RunAsync<LayerDependencyAnalyzer>(
      [("src/api/shared/Text/TextNormalizer.cs", SharedText), ("src/api/modules/boards/Board.cs", BoardsModule), .. files]);
}
