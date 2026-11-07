namespace Coordina.Quality.Checks;

/// <summary>Where each kind of code lives in this repository.</summary>
internal static class RepositoryPaths
{
  private const string ModulesRoot = "src/api/modules/";

  private static readonly string[] GeneratedMarkers =
  [
    "/Migrations/",
    "/bin/",
    "/obj/",
    "/node_modules/",
    "/dist/"
  ];

  public static bool IsGenerated(string path) =>
    GeneratedMarkers.Any(marker => path.Contains(marker, StringComparison.Ordinal))
    || path.EndsWith(".Designer.cs", StringComparison.Ordinal);

  public static bool IsCSharp(string path) =>
    path.EndsWith(".cs", StringComparison.Ordinal) && !IsGenerated(path);

  public static bool IsTestCode(string path) =>
    path.StartsWith("tests/", StringComparison.Ordinal);

  public static bool IsWebSource(string path) =>
    (path.StartsWith("src/web/src/", StringComparison.Ordinal)
      || path.StartsWith("src/web/e2e/", StringComparison.Ordinal))
    && (path.EndsWith(".ts", StringComparison.Ordinal)
      || path.EndsWith(".tsx", StringComparison.Ordinal))
    && !IsGenerated(path);

  public static bool IsGraphEngineSource(string path) =>
    (path.StartsWith("src/graph-engine/", StringComparison.Ordinal)
      || path.StartsWith("tests/graph-engine/", StringComparison.Ordinal))
    && (path.EndsWith(".cpp", StringComparison.Ordinal)
      || path.EndsWith(".h", StringComparison.Ordinal))
    && !IsGenerated(path);

  public static bool IsCode(string path) =>
    IsCSharp(path) || IsWebSource(path) || IsGraphEngineSource(path);

  /// <summary>The module folder name, such as <c>boards</c>, for files inside an API module.</summary>
  public static string? ModuleOf(string path)
  {
    if (!path.StartsWith(ModulesRoot, StringComparison.Ordinal))
    {
      return null;
    }

    var rest = path[ModulesRoot.Length..];
    var slash = rest.IndexOf('/', StringComparison.Ordinal);

    return slash > 0 ? rest[..slash] : null;
  }

  /// <summary>The path inside the module: <c>Application/BoardRules.cs</c>.</summary>
  public static string PathInModule(string path, string module) =>
    path[(ModulesRoot.Length + module.Length + 1)..];
}
