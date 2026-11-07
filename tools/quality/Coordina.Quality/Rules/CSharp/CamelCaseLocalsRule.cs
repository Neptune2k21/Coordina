using Coordina.Quality.Checks;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class CamelCaseLocalsRule : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1005",
    "Locals and parameters use camelCase",
    Severity.Error,
    "camelCase separates values that live in a method from members of the type. Use '_' for a value you ignore.");

  public override IEnumerable<Violation> Check(SourceFile file) =>
    LocalNames.In(file.Root)
      .Select(local => (local.Identifier, local.Kind, Name: Naming.WithoutVerbatimPrefix(local.Identifier.ValueText)))
      .Where(local => !Naming.IsDiscard(local.Name) && !Naming.IsCamelCase(local.Name))
      .Select(local => At(
        file,
        SourceFile.LineOf(local.Identifier),
        $"Rename {local.Kind} '{local.Name}' to camelCase."));
}
