using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class PascalCaseRule : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1001",
    "Types and members use PascalCase",
    Severity.Error,
    "One casing per kind of name makes code quick to scan. PascalCase is the .NET convention for types, members and constants. Test methods may separate parts with underscores.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var isTest = RepositoryPaths.IsTestCode(file.Path);

    foreach (var node in file.Root.DescendantNodes())
    {
      foreach (var (identifier, kind) in NamesOf(node))
      {
        var name = Naming.WithoutVerbatimPrefix(identifier.ValueText);
        var isValid = Naming.IsPascalCase(name)
          || (isTest && kind == "method" && Naming.IsTestMethodName(name));

        if (!isValid)
        {
          yield return At(
            file,
            SourceFile.LineOf(identifier),
            $"Rename {kind} '{name}' to PascalCase.");
        }
      }
    }
  }

  private static IEnumerable<(SyntaxToken Identifier, string Kind)> NamesOf(SyntaxNode node)
  {
    switch (node)
    {
      case BaseTypeDeclarationSyntax type:
        yield return (type.Identifier, "type");
        break;
      case DelegateDeclarationSyntax declaration:
        yield return (declaration.Identifier, "delegate");
        break;
      case MethodDeclarationSyntax method:
        yield return (method.Identifier, "method");
        break;
      case LocalFunctionStatementSyntax function:
        yield return (function.Identifier, "local function");
        break;
      case PropertyDeclarationSyntax property:
        yield return (property.Identifier, "property");
        break;
      case EventDeclarationSyntax declaration:
        yield return (declaration.Identifier, "event");
        break;
      case EnumMemberDeclarationSyntax member:
        yield return (member.Identifier, "enum member");
        break;
      case ParameterSyntax parameter when parameter.Parent?.Parent is RecordDeclarationSyntax:
        yield return (parameter.Identifier, "record property");
        break;
      case FieldDeclarationSyntax field
        when field.Modifiers.Any(SyntaxKind.ConstKeyword) || !IsPrivate(field.Modifiers):
        foreach (var variable in field.Declaration.Variables)
        {
          yield return (variable.Identifier, field.Modifiers.Any(SyntaxKind.ConstKeyword)
            ? "constant"
            : "field");
        }

        break;
      case LocalDeclarationStatementSyntax local when local.IsConst:
        foreach (var variable in local.Declaration.Variables)
        {
          yield return (variable.Identifier, "constant");
        }

        break;
    }
  }
}
