using System.Text.Json;
using Coordina.Quality.Baselines;
using Coordina.Quality.Checks;
using Coordina.Quality.Reporting;

namespace Coordina.Quality.Tests.Reporting;

public sealed class SarifReportTests
{
  private static readonly RuleInfo Error = new("CQ0100", "An error", Severity.Error, "Errors matter.");
  private static readonly RuleInfo Warning = new("CQ0200", "A warning", Severity.Warning, "Worth a look.");

  [Fact]
  public void Write_DescribesEachRuleOnceWithItsRationale()
  {
    var rules = Run(Report(added: [At(Error, 1), At(Error, 2)], warnings: [At(Warning, 3)]))
      .GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray()
      .Select(rule => (
        rule.GetProperty("id").GetString(),
        rule.GetProperty("fullDescription").GetProperty("text").GetString(),
        rule.GetProperty("defaultConfiguration").GetProperty("level").GetString()));

    Assert.Equal(
      [("CQ0100", "Errors matter.", "error"), ("CQ0200", "Worth a look.", "warning")],
      rules);
  }

  [Fact]
  public void Write_LeavesWarningsOutOfTheBaseline()
  {
    var result = Run(Report(warnings: [At(Warning, 3)])).GetProperty("results")[0];

    Assert.Equal("warning", result.GetProperty("level").GetString());
    Assert.False(result.TryGetProperty("baselineState", out _));
  }

  [Fact]
  public void Write_KeepsFingerprintsWhenLinesMove_AndTellsIdenticalViolationsApart()
  {
    var before = Fingerprints(Report(added: [At(Error, 10), At(Error, 20)]));
    var after = Fingerprints(Report(added: [At(Error, 15), At(Error, 25)]));

    Assert.Equal(2, before.Distinct().Count());
    Assert.Equal(before, after);
  }

  private static CheckResult Report(
    IReadOnlyList<Violation>? added = null,
    IReadOnlyList<Violation>? warnings = null) =>
    new("the whole repository", 1, new BaselineComparison([], added ?? []), warnings ?? [], []);

  private static Violation At(RuleInfo rule, int line) => new(rule, "src/a.cs", line, "Same message.");

  private static JsonElement Run(CheckResult result) =>
    JsonDocument.Parse(SarifReport.Write(result)).RootElement.GetProperty("runs")[0].Clone();

  private static string?[] Fingerprints(CheckResult result) =>
    [.. Run(result).GetProperty("results").EnumerateArray()
      .Select(entry => entry.GetProperty("partialFingerprints").GetProperty("coordinaQuality/v1").GetString())];
}
