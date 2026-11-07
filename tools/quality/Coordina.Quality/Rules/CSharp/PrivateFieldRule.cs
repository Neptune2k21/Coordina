using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class PrivateFieldRule : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1004",
    "Private fields use _camelCase, static readonly fields PascalCase",
    Severity.Error,
    "The underscore separates state from locals and parameters without needing 'this.'. Static readonly fields behave like constants, so they are named like constants.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    foreach (var field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
    {
      if (field.Modifiers.Any(SyntaxKind.ConstKeyword)
        || !IsPrivate(field.Modifiers)
        || field.Parent is StructDeclarationSyntax { Modifiers: var structModifiers }
          && structModifiers.Any(SyntaxKind.ReadOnlyKeyword))
      {
        continue;
      }

      var isStaticReadonly = field.Modifiers.Any(SyntaxKind.StaticKeyword)
        && field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

      foreach (var variable in field.Declaration.Variables)
      {
        var name = variable.Identifier.ValueText;

        if (isStaticReadonly && !Naming.IsPascalCase(name))
        {
          yield return At(
            file,
            SourceFile.LineOf(variable.Identifier),
            $"Rename static readonly field '{name}' to PascalCase.");
        }
        else if (!isStaticReadonly && !Naming.IsPrivateFieldName(name))
        {
          yield return At(
            file,
            SourceFile.LineOf(variable.Identifier),
            $"Rename private field '{name}' to _camelCase.");
        }
      }
    }
  }
}
