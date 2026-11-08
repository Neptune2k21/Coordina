namespace Coordina.Quality.Impact;

/// <summary>
/// Decides which suites a change needs, and says why. The same answer drives
/// <c>make verify</c> locally and the jobs in CI, so they cannot disagree.
/// Anything unrecognised runs everything: a rule that is too broad costs
/// minutes, one that is too narrow lets a regression through.
/// </summary>
internal static class ImpactAnalysis
{
  private static readonly ImpactRule[] Rules =
  [
    new(
      path => path.StartsWith("docs/", StringComparison.Ordinal)
        || path.StartsWith(".github/ISSUE_TEMPLATE/", StringComparison.Ordinal)
        || path is "docs.json" or "LICENSE" or ".github/pull_request_template.md" or ".github/CODEOWNERS"
        || (!path.Contains('/', StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal)),
      [],
      "documentation only"),
    new(
      path => path.StartsWith("tools/quality/", StringComparison.Ordinal)
        || path.StartsWith("tests/quality/", StringComparison.Ordinal)
        || path.StartsWith(".quality/", StringComparison.Ordinal)
        || path.StartsWith(".githooks/", StringComparison.Ordinal),
      [Suite.Quality],
      "quality tool"),
    new(
      path => path.StartsWith("src/graph-engine/", StringComparison.Ordinal)
        || path.StartsWith("tests/graph-engine/", StringComparison.Ordinal),
      [Suite.GraphEngine, Suite.Api, Suite.EndToEnd],
      "graph engine, which the API calls through native interop"),
    new(
      path => path.StartsWith("tools/analyzers/", StringComparison.Ordinal)
        || path.StartsWith("tests/analyzers/", StringComparison.Ordinal),
      [Suite.Api],
      "Roslyn analyzers, which run when the API builds"),
    new(
      path => path == "contracts/graph-engine-abi.json",
      [Suite.GraphEngine, Suite.Api],
      "native ABI contract, which the graph engine and the API both check"),
    new(
      path => path.StartsWith("contracts/", StringComparison.Ordinal),
      [Suite.Api, Suite.Web, Suite.EndToEnd],
      "API contract, which the web app depends on"),
    new(
      path => path.StartsWith("src/api/", StringComparison.Ordinal)
        || path.StartsWith("tests/api/", StringComparison.Ordinal)
        || path.StartsWith("tests/api-postgres/", StringComparison.Ordinal)
        || path.StartsWith(".config/", StringComparison.Ordinal),
      [Suite.Api, Suite.EndToEnd],
      "API; the web app only needs checking when contracts/openapi.json changes too"),
    new(
      path => path is "src/web/package.json" or "src/web/pnpm-lock.yaml",
      [Suite.Quality, Suite.Web, Suite.EndToEnd],
      "web dependencies, which include the jscpd and ESLint the quality tool runs"),
    new(
      path => path.StartsWith("src/web/", StringComparison.Ordinal),
      [Suite.Web, Suite.EndToEnd],
      "web app")
  ];

  public static ImpactPlan Analyze(IEnumerable<string> changedPaths)
  {
    var reasons = new Dictionary<Suite, SortedSet<string>>();

    foreach (var path in changedPaths)
    {
      var rule = Rules.FirstOrDefault(candidate => candidate.Matches(path));
      var (suites, reason) = rule is null
        ? (Suite.All, "shared build or CI configuration (make, workflows, Docker, solution)")
        : (rule.Suites, rule.Reason);

      foreach (var suite in suites)
      {
        if (!reasons.TryGetValue(suite, out var set))
        {
          set = new SortedSet<string>(StringComparer.Ordinal);
          reasons[suite] = set;
        }

        set.Add(reason);
      }
    }

    return new ImpactPlan(
      Suite.All.Where(reasons.ContainsKey).ToArray(),
      reasons.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<string>)pair.Value));
  }

  public static ImpactPlan Only(Suite suite, string reason) =>
    new([suite], new Dictionary<Suite, IReadOnlyCollection<string>> { [suite] = [reason] });

  public static ImpactPlan Everything(string reason) =>
    new(Suite.All, Suite.All.ToDictionary(suite => suite, _ => (IReadOnlyCollection<string>)[reason]));

  private sealed record ImpactRule(
    Func<string, bool> Matches,
    IReadOnlyList<Suite> Suites,
    string Reason);
}
