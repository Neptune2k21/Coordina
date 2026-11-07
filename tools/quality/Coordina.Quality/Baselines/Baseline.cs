using System.Text.Encodings.Web;
using System.Text.Json;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Baselines;

/// <summary>
/// Known errors at the time the baseline was written. Only errors beyond the
/// recorded count for the same rule, file and message fail a check, so the
/// existing debt does not block work while new debt cannot get in. Fixing an
/// error lets the baseline shrink; it is never meant to grow.
/// </summary>
internal sealed class Baseline
{
  public const string RelativePath = ".quality/baseline.json";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    // The file is read in code review, so keep quotes and accents as they are.
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  private readonly Dictionary<string, int> _counts;

  private Baseline(IEnumerable<BaselineEntry> entries)
  {
    Entries = entries
      .OrderBy(entry => entry.Path, StringComparer.Ordinal)
      .ThenBy(entry => entry.Rule, StringComparer.Ordinal)
      .ThenBy(entry => entry.Message, StringComparer.Ordinal)
      .ToArray();
    _counts = Entries.ToDictionary(entry => entry.Key, entry => entry.Count, StringComparer.Ordinal);
  }

  public IReadOnlyList<BaselineEntry> Entries { get; }

  public static Baseline Empty { get; } = new([]);

  public static Baseline From(IEnumerable<Violation> violations) =>
    new(violations
      .Where(violation => violation.Rule.Severity == Severity.Error)
      .GroupBy(violation => (violation.Rule.Id, violation.Path, violation.Message))
      .Select(group => new BaselineEntry(group.Key.Id, group.Key.Path, group.Key.Message, group.Count())));

  public static Baseline Load(string repositoryRoot)
  {
    var path = Path.Combine(repositoryRoot, RelativePath);

    if (!File.Exists(path))
    {
      return Empty;
    }

    var document = JsonSerializer.Deserialize<BaselineDocument>(File.ReadAllText(path), JsonOptions);

    return new Baseline(document?.Entries ?? []);
  }

  public void Save(string repositoryRoot)
  {
    var path = Path.Combine(repositoryRoot, RelativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(new BaselineDocument(1, Entries), JsonOptions) + "\n");
  }

  /// <summary>Splits errors into the ones the baseline already knows and the new ones.</summary>
  public BaselineComparison Compare(IEnumerable<Violation> violations)
  {
    var known = new List<Violation>();
    var added = new List<Violation>();

    foreach (var group in violations
      .Where(violation => violation.Rule.Severity == Severity.Error)
      .GroupBy(violation => violation.Key))
    {
      var allowed = _counts.GetValueOrDefault(group.Key);
      var ordered = group.OrderBy(violation => violation.Line).ToArray();

      known.AddRange(ordered.Take(allowed));
      added.AddRange(ordered.Skip(allowed));
    }

    return new BaselineComparison(known, added);
  }

  /// <summary>Entries for the given files that no longer happen, so the baseline can shrink.</summary>
  public IReadOnlyList<BaselineEntry> Resolved(
    IEnumerable<Violation> violations,
    IReadOnlySet<string> checkedPaths)
  {
    var current = violations
      .GroupBy(violation => violation.Key)
      .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    return Entries
      .Where(entry => checkedPaths.Contains(entry.Path)
        && current.GetValueOrDefault(entry.Key) < entry.Count)
      .ToArray();
  }

  private sealed record BaselineDocument(int Version, IReadOnlyList<BaselineEntry> Entries);
}
