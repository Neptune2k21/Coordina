using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class MethodLengthRule : CSharpRule
{
  public const int MaxLines = 60;

  public override RuleInfo Info { get; } = new(
    "CQ1302",
    $"Methods stay under {MaxLines} lines",
    Severity.Warning,
    "A method that does not fit on a screen usually does several things. Splitting it gives each step a name and makes it testable on its own. This is a warning: some methods, like long switch mappings, are fine as they are.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    foreach (var member in file.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
      var body = (SyntaxNode?)member.Body ?? member.ExpressionBody;

      if (body is null)
      {
        continue;
      }

      var span = body.GetLocation().GetLineSpan();
      var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

      if (lines > MaxLines)
      {
        var name = member switch
        {
          MethodDeclarationSyntax method => method.Identifier.ValueText,
          ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
          _ => member.Kind().ToString()
        };

        yield return At(
          file,
          SourceFile.LineOf(member),
          $"'{name}' is {lines} lines long (limit {MaxLines}). Extract the steps into named methods.");
      }
    }
  }
}
