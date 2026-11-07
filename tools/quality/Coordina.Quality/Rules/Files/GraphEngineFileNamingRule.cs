using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.Files;

internal sealed class GraphEngineFileNamingRule : Rule
{
  public override RuleInfo Info { get; } = new(
    "CQ3001",
    "Graph engine files and folders use snake_case",
    Severity.Error,
    "The C++ engine follows the standard library's snake_case style, and its file names match the names inside them.");

  public override bool AppliesTo(SourceFile file) => RepositoryPaths.IsGraphEngineSource(file.Path);

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    if (!Naming.IsSnakeCase(file.Stem))
    {
      yield return At(file, 1, $"Rename '{file.FileName}' to snake_case.");
    }

    foreach (var folder in file.Directory.Split('/').Skip(2).Where(folder => !Naming.IsSnakeCase(folder)))
    {
      yield return At(file, 1, $"Rename folder '{folder}' to snake_case.");
    }
  }
}
