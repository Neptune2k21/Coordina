using Coordina.Quality.Baselines;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Tests.Baselines;

public sealed class BaselineTests
{
  private static readonly RuleInfo Error = new("CQ9001", "Error rule", Severity.Error, "Because.");
  private static readonly RuleInfo Warning = new("CQ9002", "Warning rule", Severity.Warning, "Because.");

  [Fact]
  public void KnownErrors_DoNotFail_ButExtraOnesDo()
  {
    var baseline = Baseline.From([At(1, "Same message"), At(5, "Same message")]);

    var comparison = baseline.Compare(
    [
      At(2, "Same message"),
      At(7, "Same message"),
      At(9, "Same message"),
      At(3, "Another message")
    ]);

    Assert.Equal([2, 7], comparison.Known.Select(violation => violation.Line));
    Assert.Equal(
      ["Same message", "Another message"],
      comparison.Added.Select(violation => violation.Message).Order(StringComparer.Ordinal).Reverse());
  }

  [Fact]
  public void Warnings_NeverEnterTheBaselineOrFail()
  {
    var baseline = Baseline.From([new Violation(Warning, "a.cs", 1, "Long")]);

    Assert.Empty(baseline.Entries);
    Assert.Empty(baseline.Compare([new Violation(Warning, "a.cs", 1, "Long")]).Added);
  }

  [Fact]
  public void Resolved_ListsFixedEntriesInCheckedFilesOnly()
  {
    var baseline = Baseline.From([At(1, "Fixed"), At(2, "Still there"), At(3, "Other file", "b.cs")]);

    var resolved = baseline.Resolved([At(2, "Still there")], new HashSet<string>(["a.cs"]));

    Assert.Equal(["Fixed"], resolved.Select(entry => entry.Message));
  }

  [Fact]
  public void SaveAndLoad_RoundTripsSortedEntries()
  {
    var root = Directory.CreateTempSubdirectory("quality-baseline-").FullName;

    try
    {
      Baseline.From([At(1, "B", "b.cs"), At(1, "A", "a.cs"), At(2, "A", "a.cs")]).Save(root);

      var loaded = Baseline.Load(root);

      Assert.Equal(
        [("a.cs", "A", 2), ("b.cs", "B", 1)],
        loaded.Entries.Select(entry => (entry.Path, entry.Message, entry.Count)));
      Assert.Empty(Baseline.Load(Path.Combine(root, "missing")).Entries);
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static Violation At(int line, string message, string path = "a.cs") =>
    new(Error, path, line, message);
}
