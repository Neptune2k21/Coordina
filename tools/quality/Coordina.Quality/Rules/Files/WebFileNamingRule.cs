using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.Files;

internal sealed class WebFileNamingRule : Rule
{
  // The root component keeps React's conventional name, and its test follows it.
  private const string RootComponentStem = "App";

  public override RuleInfo Info { get; } = new(
    "CQ2001",
    "Web files and folders use kebab-case",
    Severity.Error,
    "Case-insensitive file systems (macOS, Windows) treat 'Board.tsx' and 'board.tsx' as the same file, which breaks imports in confusing ways. kebab-case avoids the question entirely.");

  public override bool AppliesTo(SourceFile file) =>
    RepositoryPaths.IsWebSource(file.Path)
    && !(file.Directory == "src/web/src" && file.Stem == RootComponentStem);

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    if (!Naming.IsKebabCase(file.Stem))
    {
      yield return At(file, 1, $"Rename '{file.FileName}' to kebab-case.");
    }

    var folders = file.Directory.Split('/').Skip(3);

    foreach (var folder in folders.Where(folder => !Naming.IsKebabCase(folder)))
    {
      yield return At(file, 1, $"Rename folder '{folder}' to kebab-case.");
    }
  }
}
