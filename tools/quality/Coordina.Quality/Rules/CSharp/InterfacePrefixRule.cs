using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class InterfacePrefixRule : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1002",
    "Interfaces start with I",
    Severity.Error,
    "The I prefix tells a reader at the call site that a type is a contract, not an implementation.");

  public override IEnumerable<Violation> Check(SourceFile file) =>
    file.Root.DescendantNodes()
      .OfType<InterfaceDeclarationSyntax>()
      .Select(declaration => declaration.Identifier)
      .Where(identifier =>
      {
        var name = identifier.ValueText;
        return name.Length < 2 || name[0] != 'I' || !char.IsUpper(name[1]);
      })
      .Select(identifier => At(
        file,
        SourceFile.LineOf(identifier),
        $"Rename interface '{identifier.ValueText}' to start with 'I', such as 'I{Naming.ToPascalCase(identifier.ValueText)}'."));
}
