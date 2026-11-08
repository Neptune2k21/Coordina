using Coordina.Analyzers.Tests.Support;

namespace Coordina.Analyzers.Tests;

/// <summary>The rules every module follows: CQ6002 to CQ6005.</summary>
public sealed class ModuleConventionAnalyzerTests
{
  [Fact]
  public async Task SystemClock_IsReportedInModules_Only()
  {
    var diagnostics = await AnalyzerHarness.RunAsync<SystemClockAnalyzer>(
      ("src/api/modules/boards/Due.cs",
        """
        using System;

        namespace Coordina.Api.Modules.Boards;

        public static class Due
        {
          public static DateTime Now() => DateTime.UtcNow;

          public static DateTimeOffset Today() => DateTimeOffset.Now;

          public static DateTime Never() => DateTime.MaxValue;

          public static DateTimeOffset From(TimeProvider time) => time.GetUtcNow();
        }
        """),
      ("src/api/core/Time/SystemTime.cs",
        """
        using System;

        namespace Coordina.Api.Core.Time;

        public static class SystemTime
        {
          public static DateTime Now() => DateTime.UtcNow;
        }
        """));

    Assert.Equal(["CQ6002 src/api/modules/boards/Due.cs:7", "CQ6002 src/api/modules/boards/Due.cs:9"], diagnostics);
  }

  [Fact]
  public async Task ClaimParsing_IsReportedOutsideTheAuthModule()
  {
    const string Reader =
      """
      using System.Security.Claims;

      namespace Coordina.Api.Modules.MODULE;

      public static class UserReader
      {
        public static string? Id(ClaimsPrincipal user) => user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        public static bool IsSignedIn(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;
      }
      """;

    var diagnostics = await AnalyzerHarness.RunAsync<ClaimParsingAnalyzer>(
      ("src/api/modules/boards/UserReader.cs", Reader.Replace("MODULE", "Boards", StringComparison.Ordinal)),
      ("src/api/modules/auth/UserReader.cs", Reader.Replace("MODULE", "Auth", StringComparison.Ordinal)));

    Assert.Equal(
      ["CQ6003 src/api/modules/boards/UserReader.cs:7", "CQ6003 src/api/modules/boards/UserReader.cs:7"],
      diagnostics);
  }

  [Fact]
  public async Task DbContext_IsReportedOutsideInfrastructure()
  {
    const string Store =
      """
      using Coordina.Api.Core.Persistence;

      namespace Coordina.Api.Modules.Boards.LAYER;

      public sealed class BoardStore(CoordinaDbContext context)
      {
        public CoordinaDbContext Context { get; } = context;
      }
      """;

    var diagnostics = await AnalyzerHarness.RunAsync<DbContextPlacementAnalyzer>(
      ("src/api/modules/boards/Application/BoardStore.cs", Store.Replace("LAYER", "Application", StringComparison.Ordinal)),
      ("src/api/modules/boards/Infrastructure/BoardStore.cs", Store.Replace("LAYER", "Infrastructure", StringComparison.Ordinal)));

    Assert.Equal(
      ["CQ6004 src/api/modules/boards/Application/BoardStore.cs:5", "CQ6004 src/api/modules/boards/Application/BoardStore.cs:7"],
      diagnostics);
  }

  [Fact]
  public async Task ResultTypes_AreReportedWhenAModuleDeclaresThem()
  {
    var diagnostics = await AnalyzerHarness.RunAsync<ModuleResultTypeAnalyzer>(
      ("src/api/modules/boards/Moves.cs",
        """
        namespace Coordina.Api.Modules.Boards;

        public sealed record MoveResult(bool Moved);

        public enum MoveResultStatus { Moved, Blocked }

        public static class ResultMapper;
        """),
      ("src/api/core/Results/Result.cs",
        """
        namespace Coordina.Api.Core.Results;

        public sealed record Result(bool IsSuccess);
        """));

    Assert.Equal(["CQ6005 src/api/modules/boards/Moves.cs:3", "CQ6005 src/api/modules/boards/Moves.cs:5"], diagnostics);
  }
}
