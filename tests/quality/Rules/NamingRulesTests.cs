using Coordina.Quality.Rules.CSharp;
using Coordina.Quality.Tests.Support;

namespace Coordina.Quality.Tests.Rules;

public sealed class NamingRulesTests
{
  private const string ApiFile = "src/api/modules/boards/Application/Sample.cs";
  private const string TestFile = "tests/api/Unit/Boards/SampleTests.cs";

  [Fact]
  public void PascalCase_FlagsTypesMembersAndConstants()
  {
    var messages = RuleHarness.Messages(new PascalCaseRule(), ApiFile, """
      namespace X;
      public sealed class sample
      {
        public const int maxCount = 3;
        public int count { get; set; }
        public void run() { }
      }
      public enum Status { active }
      public sealed record Card(string title);
      """);

    Assert.Equal(
      [
        "Rename type 'sample' to PascalCase.",
        "Rename constant 'maxCount' to PascalCase.",
        "Rename property 'count' to PascalCase.",
        "Rename method 'run' to PascalCase.",
        "Rename enum member 'active' to PascalCase.",
        "Rename record property 'title' to PascalCase."
      ],
      messages);
  }

  [Fact]
  public void PascalCase_AllowsUnderscoresInTestMethodsOnly()
  {
    const string Code = "public sealed class SampleTests { public void Create_WhenEmpty_Fails() { } }";

    Assert.Empty(RuleHarness.Check(new PascalCaseRule(), TestFile, Code));
    Assert.Single(RuleHarness.Check(new PascalCaseRule(), ApiFile, Code));
  }

  [Fact]
  public void InterfacePrefix_FlagsInterfacesWithoutI()
  {
    var messages = RuleHarness.Messages(new InterfacePrefixRule(), ApiFile, """
      public interface IBoardStore { }
      public interface BoardReader { }
      public interface Iterator { }
      """);

    Assert.Equal(2, messages.Length);
    Assert.Contains("'BoardReader'", messages[0], StringComparison.Ordinal);
    Assert.Contains("'Iterator'", messages[1], StringComparison.Ordinal);
  }

  [Fact]
  public void AsyncSuffix_FlagsAwaitableMethodsWithoutTheSuffix()
  {
    var messages = RuleHarness.Messages(new AsyncSuffixRule(), ApiFile, """
      public sealed class Sample
      {
        public Task Save() => Task.CompletedTask;
        public async Task<int> Load() => await Task.FromResult(1);
        public ValueTask<int> Count() => new(1);
        public Task SaveAsync() => Task.CompletedTask;
        public int Total() => 1;
        public override Task ToStringTask() => Task.CompletedTask;
      }
      """);

    Assert.Equal(
      [
        "Rename 'Save' to 'SaveAsync': it returns an awaitable.",
        "Rename 'Load' to 'LoadAsync': it returns an awaitable.",
        "Rename 'Count' to 'CountAsync': it returns an awaitable."
      ],
      messages);
  }

  [Fact]
  public void AsyncSuffix_ExemptsTestsAndRouteHandlers()
  {
    const string Test = "public sealed class T { [Fact] public async Task Saves() => await Task.CompletedTask; }";
    const string Handler = "public static class BoardsEndpoints { private static Task<int> GetBoard() => Task.FromResult(1); }";

    Assert.Empty(RuleHarness.Check(new AsyncSuffixRule(), TestFile, Test));
    Assert.Empty(RuleHarness.Check(new AsyncSuffixRule(), "src/api/modules/boards/BoardsEndpoints.cs", Handler));
  }

  [Fact]
  public void PrivateFields_UseUnderscoreOrPascalCaseWhenStaticReadonly()
  {
    var messages = RuleHarness.Messages(new PrivateFieldRule(), ApiFile, """
      public sealed class Sample
      {
        private readonly int _count;
        private readonly int value;
        private static readonly string[] Codes = [];
        private static readonly string[] _codes = [];
        private const int Limit = 1;
        public int Exposed;
      }
      """);

    Assert.Equal(
      [
        "Rename private field 'value' to _camelCase.",
        "Rename static readonly field '_codes' to PascalCase."
      ],
      messages);
  }

  [Fact]
  public void Locals_UseCamelCaseAndAllowDiscards()
  {
    var messages = RuleHarness.Messages(new CamelCaseLocalsRule(), ApiFile, """
      public sealed class Sample
      {
        public void Run(int Count, int _)
        {
          var Total = 1;
          foreach (var Item in new[] { 1 }) { }
          if (Total is int Matched) { }
          _ = new[] { 1 }.Select(_ => 1);
        }
      }
      """);

    Assert.Equal(
      [
        "Rename parameter 'Count' to camelCase.",
        "Rename variable 'Total' to camelCase.",
        "Rename loop variable 'Item' to camelCase.",
        "Rename variable 'Matched' to camelCase."
      ],
      messages);
  }

  [Fact]
  public void DescriptiveNames_FlagsOneLetterAndVagueNames()
  {
    var messages = RuleHarness.Messages(new DescriptiveNamesRule(), ApiFile, """
      public sealed class Sample
      {
        public void Run(int x)
        {
          var tmp = 1;
          var ui = 2;
          for (var i = 0; i < 3; i++) { }
          _ = new[] { 1 }.Select(c => c);
        }
      }
      """);

    Assert.Equal(
      [
        "Give parameter 'x' a name that says what it holds.",
        "Give variable 'tmp' a name that says what it holds.",
        "Give parameter 'c' a name that says what it holds."
      ],
      messages);
  }
}
