using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class DescriptiveNamesRule : CSharpRule
{
  // Loop counters are universally understood. Every other one-letter name gets a real name.
  private static readonly HashSet<string> AllowedSingleLetters = ["i", "j", "k"];

  private static readonly HashSet<string> VagueNames =
  [
    "tmp", "temp", "foo", "bar", "baz", "obj", "val", "ret", "res", "str",
    "num", "arr", "lst", "dat", "stuff", "thing", "thingy", "misc", "data1",
    "data2", "item1", "item2", "var1", "var2", "test1", "test2"
  ];

  public override RuleInfo Info { get; } = new(
    "CQ1006",
    "Names describe what they hold",
    Severity.Error,
    "Code is read far more often than written. 'card', 'board' or 'dependency' explain themselves; 'x', 'tmp' or 'obj' make every reader work it out again. One-letter names are reserved for the loop counters i, j and k.");

  public override IEnumerable<Violation> Check(SourceFile file) =>
    LocalNames.In(file.Root)
      .Select(local => (local.Identifier, local.Kind, Name: Naming.WithoutVerbatimPrefix(local.Identifier.ValueText)))
      .Where(local => !Naming.IsDiscard(local.Name) && IsVague(local.Name))
      .Select(local => At(
        file,
        SourceFile.LineOf(local.Identifier),
        $"Give {local.Kind} '{local.Name}' a name that says what it holds."));

  private static bool IsVague(string name) =>
    (name.Length == 1 && !AllowedSingleLetters.Contains(name))
    || VagueNames.Contains(name.ToLowerInvariant());
}
