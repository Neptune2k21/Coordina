using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Reporting;

/// <summary>
/// Writes a check as SARIF 2.1.0, the format GitHub code scanning reads, so
/// every violation, the known ones included, is listed in the repository's
/// Security tab. Known errors are marked <c>unchanged</c> and new ones
/// <c>new</c>, as the baseline sees them.
/// </summary>
internal static class SarifReport
{
  public const string ToolName = "coordina-quality";

  private const string FingerprintName = "coordinaQuality/v1";

  private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

  public static string Write(CheckResult result)
  {
    var violations = result.Comparison.Added.Select(violation => new Entry(violation, "new"))
      .Concat(result.Comparison.Known.Select(violation => new Entry(violation, "unchanged")))
      .Concat(result.Warnings.Select(violation => new Entry(violation, null)))
      .OrderBy(entry => entry.Violation.Path, StringComparer.Ordinal)
      .ThenBy(entry => entry.Violation.Line)
      .ToArray();
    var rules = violations
      .Select(entry => entry.Violation.Rule)
      .DistinctBy(rule => rule.Id)
      .OrderBy(rule => rule.Id, StringComparer.Ordinal)
      .ToArray();
    var ruleIndexes = rules
      .Select((rule, index) => (rule.Id, index))
      .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
    var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

    var report = new JsonObject
    {
      ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
      ["version"] = "2.1.0",
      ["runs"] = new JsonArray(new JsonObject
      {
        ["tool"] = new JsonObject
        {
          ["driver"] = new JsonObject
          {
            ["name"] = ToolName,
            ["informationUri"] = "https://github.com/Neptune2k21/Coordina/blob/master/CONTRIBUTING.md",
            ["rules"] = new JsonArray([.. rules.Select(Rule)])
          }
        },
        ["results"] = new JsonArray([.. violations.Select(entry =>
          Result(entry, ruleIndexes[entry.Violation.Rule.Id], occurrences))])
      })
    };

    return report.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
  }

  private static JsonObject Rule(RuleInfo rule) =>
    new()
    {
      ["id"] = rule.Id,
      ["shortDescription"] = new JsonObject { ["text"] = rule.Title },
      ["fullDescription"] = new JsonObject { ["text"] = rule.Rationale },
      ["defaultConfiguration"] = new JsonObject { ["level"] = Level(rule.Severity) }
    };

  private static JsonObject Result(Entry entry, int ruleIndex, Dictionary<string, int> occurrences)
  {
    var (violation, baselineState) = entry;
    var result = new JsonObject
    {
      ["ruleId"] = violation.Rule.Id,
      ["ruleIndex"] = ruleIndex,
      ["level"] = Level(violation.Rule.Severity),
      ["message"] = new JsonObject { ["text"] = violation.Message },
      ["locations"] = new JsonArray(new JsonObject
      {
        ["physicalLocation"] = new JsonObject
        {
          ["artifactLocation"] = new JsonObject
          {
            ["uri"] = violation.Path,
            ["uriBaseId"] = "%SRCROOT%"
          },
          ["region"] = new JsonObject { ["startLine"] = Math.Max(violation.Line, 1) }
        }
      }),
      ["partialFingerprints"] = new JsonObject
      {
        [FingerprintName] = Fingerprint(violation, occurrences)
      }
    };

    if (baselineState is not null)
    {
      result["baselineState"] = baselineState;
    }

    return result;
  }

  /// <summary>
  /// Identifies a violation without its line, like the baseline does, so code
  /// scanning keeps the same alert when code above it moves. Identical
  /// violations in one file are told apart by their order.
  /// </summary>
  private static string Fingerprint(Violation violation, Dictionary<string, int> occurrences)
  {
    var occurrence = occurrences.GetValueOrDefault(violation.Key);
    occurrences[violation.Key] = occurrence + 1;

    var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{violation.Key}|{occurrence}"));

    return Convert.ToHexStringLower(hash.AsSpan(0, 16));
  }

  private static string Level(Severity severity) =>
    severity == Severity.Error ? "error" : "warning";

  /// <summary>A violation and, for errors, whether the baseline already knew it.</summary>
  private sealed record Entry(Violation Violation, string? BaselineState);
}
