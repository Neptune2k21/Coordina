using System.Text.RegularExpressions;

namespace Coordina.Api.Tests.Unit.Architecture;

/// <summary>
/// Guards the API layering: modules → shared → core. Core is the kernel every
/// module plugs into, so it must never reach into shared helpers or modules.
/// </summary>
public sealed partial class LayerDependencyTests
{
  [Fact]
  public void Core_DoesNotDependOnSharedOrModules()
  {
    var violations = FindReferences("core", CoreForbiddenReference());

    Assert.True(violations.Length == 0, Describe(violations));
  }

  [Fact]
  public void Shared_DoesNotDependOnModules()
  {
    var violations = FindReferences("shared", ModuleReference());

    Assert.True(violations.Length == 0, Describe(violations));
  }

  private static string[] FindReferences(string layer, Regex forbidden)
  {
    var layerRoot = Path.Combine(FindApiRoot(), layer);

    Assert.True(Directory.Exists(layerRoot), $"Missing layer folder '{layerRoot}'.");

    return Directory
      .EnumerateFiles(layerRoot, "*.cs", SearchOption.AllDirectories)
      // EF migrations are generated from the composed model and name module
      // entities by design; they are the only allowed exception.
      .Where(path => !path.Contains(
        $"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
        StringComparison.Ordinal))
      .SelectMany(path => File.ReadLines(path)
        .Select((line, index) => (line, index))
        .Where(entry => forbidden.IsMatch(entry.line))
        .Select(entry => $"{Path.GetRelativePath(layerRoot, path)}:{entry.index + 1}: {entry.line.Trim()}"))
      .ToArray();
  }

  private static string FindApiRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null
      && !File.Exists(Path.Combine(directory.FullName, "Coordina.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return Path.Combine(directory.FullName, "src", "api");
  }

  private static string Describe(string[] violations) =>
    "Forbidden layer references:" + Environment.NewLine
      + string.Join(Environment.NewLine, violations);

  [GeneratedRegex(@"\bCoordina\.Api\.(Shared|Modules)\b")]
  private static partial Regex CoreForbiddenReference();

  [GeneratedRegex(@"\bCoordina\.Api\.Modules\b")]
  private static partial Regex ModuleReference();
}
