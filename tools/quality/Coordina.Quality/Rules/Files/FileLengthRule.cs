using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.Files;

internal sealed class FileLengthRule : Rule
{
  public const int MaxLines = 500;

  public override RuleInfo Info { get; } = new(
    "CQ1301",
    $"Source files stay under {MaxLines} lines",
    Severity.Warning,
    "Large files tend to collect unrelated responsibilities and become merge-conflict magnets. This is a warning, to prompt a split before the file becomes hard to change.");

  public override bool AppliesTo(SourceFile file) => RepositoryPaths.IsCode(file.Path);

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var lines = file.Text.EndsWith('\n') ? file.Lines.Count - 1 : file.Lines.Count;

    if (lines > MaxLines)
    {
      yield return At(
        file,
        1,
        $"This file has {lines} lines (limit {MaxLines}). Split it by responsibility.");
    }
  }
}
