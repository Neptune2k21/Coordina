using System.Text.RegularExpressions;

namespace Coordina.Quality.Lint;

/// <summary>
/// Which engine files include which, from their <c>#include "..."</c> lines.
/// clang-tidy only sees a header through a .cpp file that includes it, so a
/// changed header is analysed through exactly the .cpp files that include it,
/// directly or not, rather than through the whole engine.
/// </summary>
internal sealed partial class IncludeGraph
{
  private readonly Dictionary<string, HashSet<string>> _includedBy;

  private IncludeGraph(Dictionary<string, HashSet<string>> includedBy)
  {
    _includedBy = includedBy;
  }

  /// <summary>
  /// Resolves each quoted include the way the compiler does: next to the
  /// including file first, then in each <c>-I</c> folder in order.
  /// </summary>
  public static IncludeGraph Build(
    IReadOnlyCollection<string> sources,
    IReadOnlyList<string> includeDirectories,
    Func<string, string> read)
  {
    var known = sources.ToHashSet(StringComparer.Ordinal);
    var includedBy = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    foreach (var source in sources)
    {
      var directory = source.Contains('/', StringComparison.Ordinal) ? source[..source.LastIndexOf('/')] : string.Empty;

      foreach (Match match in IncludePattern().Matches(read(source)))
      {
        var included = new[] { directory }
          .Concat(includeDirectories)
          .Select(folder => Normalize($"{folder}/{match.Groups["path"].Value}"))
          .FirstOrDefault(known.Contains);

        if (included is null)
        {
          continue;
        }

        if (!includedBy.TryGetValue(included, out var includers))
        {
          includers = new HashSet<string>(StringComparer.Ordinal);
          includedBy[included] = includers;
        }

        includers.Add(source);
      }
    }

    return new IncludeGraph(includedBy);
  }

  /// <summary>The .cpp files whose analysis covers <paramref name="paths"/>, in order.</summary>
  public IReadOnlyList<string> UnitsCovering(IEnumerable<string> paths)
  {
    var reached = new HashSet<string>(StringComparer.Ordinal);
    var pending = new Queue<string>(paths);

    while (pending.Count > 0)
    {
      var path = pending.Dequeue();

      if (!reached.Add(path) || !_includedBy.TryGetValue(path, out var includers))
      {
        continue;
      }

      foreach (var includer in includers)
      {
        pending.Enqueue(includer);
      }
    }

    return reached
      .Where(IsUnit)
      .Order(StringComparer.Ordinal)
      .ToArray();
  }

  public static bool IsUnit(string path) => path.EndsWith(".cpp", StringComparison.Ordinal);

  /// <summary>Resolves <c>.</c> and <c>..</c> segments: <c>src/a/../b.h</c> is <c>src/b.h</c>.</summary>
  private static string Normalize(string path)
  {
    var segments = new List<string>();

    foreach (var segment in path.Split('/'))
    {
      if (segment == ".." && segments.Count > 0)
      {
        segments.RemoveAt(segments.Count - 1);
      }
      else if (segment is not ("." or ""))
      {
        segments.Add(segment);
      }
    }

    return string.Join('/', segments);
  }

  [GeneratedRegex("""^\s*#\s*include\s*"(?<path>[^"]+)""", RegexOptions.Multiline)]
  private static partial Regex IncludePattern();
}
