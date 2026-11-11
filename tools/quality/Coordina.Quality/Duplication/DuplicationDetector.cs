using System.Globalization;
using System.Text.Json;
using Coordina.Quality.Checks;
using Coordina.Quality.Lint;

namespace Coordina.Quality.Duplication;

/// <summary>
/// Finds duplicated code with jscpd, installed with the web app's dev
/// dependencies. It always scans the whole repository, so a block copied from
/// an unchanged file is still found when only the copy changed.
/// </summary>
internal sealed class DuplicationDetector(string repositoryRoot)
{
  public const string RelativeBinaryPath = "src/web/node_modules/.bin/jscpd";

  /// <summary>Clones shorter than this many tokens are ignored, as idioms rather than copies.</summary>
  public const int MinTokens = 70;

  private static readonly string[] ScannedFolders =
  [
    "src/api",
    "src/web/src",
    "src/web/e2e",
    "src/graph-engine",
    "tests",
    "tools"
  ];

  private static readonly string[] IgnoredGlobs =
  [
    "**/Migrations/**",
    "**/bin/**",
    "**/obj/**",
    "**/node_modules/**",
    "**/*.Designer.cs",
    // Generated from contracts/openapi.json.
    "**/api-contract.ts"
  ];

  public bool IsAvailable => File.Exists(Path.Combine(repositoryRoot, RelativeBinaryPath));

  public IReadOnlyList<CodeClone> FindClones()
  {
    var output = Directory.CreateTempSubdirectory("coordina-jscpd-");

    try
    {
      var folders = ScannedFolders.Where(folder => Directory.Exists(Path.Combine(repositoryRoot, folder)));
      var result = ToolProcess.Run(
        Path.Combine(repositoryRoot, RelativeBinaryPath),
        repositoryRoot,
        folders.Concat(
        [
          "--format", "csharp,typescript,tsx,cpp",
          "--ignore", string.Join(',', IgnoredGlobs),
          "--min-tokens", MinTokens.ToString(CultureInfo.InvariantCulture),
          "--absolute",
          "--reporters", "json",
          "--output", output.FullName,
          "--silent"
        ]));

      var report = Path.Combine(output.FullName, "jscpd-report.json");

      if (!File.Exists(report))
      {
        throw new InvalidOperationException($"jscpd wrote no report: {result.StandardError}");
      }

      return Parse(File.ReadAllText(report), repositoryRoot);
    }
    finally
    {
      output.Delete(recursive: true);
    }
  }

  /// <summary>Reads jscpd's JSON report into clones with repository-relative paths.</summary>
  public static IReadOnlyList<CodeClone> Parse(string json, string repositoryRoot)
  {
    using var document = JsonDocument.Parse(json);

    if (!document.RootElement.TryGetProperty("duplicates", out var duplicates))
    {
      return [];
    }

    return duplicates.EnumerateArray()
      .Select(duplicate => new CodeClone(
        RepositoryPaths.Relative(repositoryRoot, duplicate.GetProperty("firstFile").GetProperty("name").GetString()!),
        duplicate.GetProperty("firstFile").GetProperty("start").GetInt32(),
        RepositoryPaths.Relative(repositoryRoot, duplicate.GetProperty("secondFile").GetProperty("name").GetString()!),
        duplicate.GetProperty("secondFile").GetProperty("start").GetInt32(),
        duplicate.GetProperty("lines").GetInt32()))
      .Where(clone => !RepositoryPaths.IsGenerated(clone.FirstPath)
        && !RepositoryPaths.IsGenerated(clone.SecondPath))
      .ToArray();
  }
}
