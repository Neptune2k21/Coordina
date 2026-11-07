namespace Coordina.Quality.Impact;

/// <summary>A group of checks that runs as a unit, locally through make and in CI as a job.</summary>
internal sealed record Suite(string Name, string Description, IReadOnlyList<string> MakeTargets)
{
  public static Suite Api { get; } = new(
    "api",
    ".NET format check, unit and PostgreSQL tests",
    ["api-format-check", "api-test"]);

  public static Suite Web { get; } = new(
    "web",
    "Web format check, lint, typecheck, tests and build",
    ["web-format-check", "web-lint", "web-typecheck", "web-test", "web-build"]);

  public static Suite GraphEngine { get; } = new(
    "graph-engine",
    "Native engine tests, ABI contract, sanitizers and benchmark smoke test",
    ["graph-engine-test", "graph-engine-sanitize", "graph-engine-benchmark-smoke"]);

  public static Suite EndToEnd { get; } = new(
    "e2e",
    "Playwright end-to-end tests against the real API (needs the database)",
    ["web-e2e"]);

  public static Suite Quality { get; } = new(
    "quality",
    "Tests of this quality tool",
    ["quality-test"]);

  public static IReadOnlyList<Suite> All { get; } = [Quality, GraphEngine, Api, Web, EndToEnd];
}
