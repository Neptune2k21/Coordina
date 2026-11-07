using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

internal sealed class AsyncSuffixRule : CSharpRule
{
  private static readonly string[] AwaitableTypes = ["Task", "ValueTask"];

  public override RuleInfo Info { get; } = new(
    "CQ1003",
    "Asynchronous methods end with Async",
    Severity.Error,
    "The suffix shows at the call site that the result must be awaited. Test methods, overrides and minimal API route handlers in *Endpoints.cs are exempt, because their names are chosen by the framework or the route.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    if (file.FileName.EndsWith("Endpoints.cs", StringComparison.Ordinal))
    {
      yield break;
    }

    foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
      var name = method.Identifier.ValueText;

      if (name.EndsWith("Async", StringComparison.Ordinal)
        || name == "Main"
        || method.ExplicitInterfaceSpecifier is not null
        || method.Modifiers.Any(SyntaxKind.OverrideKeyword)
        || HasAttribute(method, "Fact", "Theory")
        || !IsAwaitable(method))
      {
        continue;
      }

      yield return At(
        file,
        SourceFile.LineOf(method.Identifier),
        $"Rename '{name}' to '{name}Async': it returns an awaitable.");
    }
  }

  private static bool IsAwaitable(MethodDeclarationSyntax method) =>
    method.Modifiers.Any(SyntaxKind.AsyncKeyword)
    || AwaitableTypes.Contains(RightmostName(method.ReturnType));

  private static string RightmostName(TypeSyntax type) => type switch
  {
    QualifiedNameSyntax qualified => RightmostName(qualified.Right),
    GenericNameSyntax generic => generic.Identifier.ValueText,
    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
    _ => string.Empty
  };
}
