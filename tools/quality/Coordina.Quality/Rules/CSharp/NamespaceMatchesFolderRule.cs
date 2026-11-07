using Coordina.Quality.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coordina.Quality.Rules.CSharp;

/// <summary>
/// The expected namespace is the name of the nearest project file followed by
/// the folders below it, so <c>src/api/modules/boards/Domain</c> in
/// <c>Coordina.Api.csproj</c> gives <c>Coordina.Api.Modules.Boards.Domain</c>.
/// </summary>
internal sealed class NamespaceMatchesFolderRule(IReadOnlyCollection<string> projectFiles)
  : CSharpRule
{
  public override RuleInfo Info { get; } = new(
    "CQ1102",
    "Namespaces follow folders",
    Severity.Error,
    "When namespaces mirror folders, a using directive tells you where the code lives, and moving a file shows up as a namespace change in review.");

  public override IEnumerable<Violation> Check(SourceFile file)
  {
    var declaration = file.Root.DescendantNodes()
      .OfType<BaseNamespaceDeclarationSyntax>()
      .FirstOrDefault();
    var expected = ExpectedNamespace(file.Directory);

    // Program stays in the global namespace: WebApplicationFactory<Program> needs it there.
    if (expected is null
      || file.FileName == "Program.cs"
      || !CSharpRule.TopLevelTypes(file.Root).Any())
    {
      yield break;
    }

    if (declaration is null)
    {
      yield return At(file, 1, $"Declare 'namespace {expected};'.");
      yield break;
    }

    var actual = declaration.Name.ToString();

    if (!Matches(actual, expected))
    {
      yield return At(
        file,
        SourceFile.LineOf(declaration.Name),
        $"Change namespace '{actual}' to '{expected}', which matches the folder.");
    }
  }

  internal string? ExpectedNamespace(string directory)
  {
    var project = projectFiles
      .Select(path => (
        Directory: path.Contains('/', StringComparison.Ordinal) ? path[..path.LastIndexOf('/')] : string.Empty,
        Name: path[(path.LastIndexOf('/') + 1)..^".csproj".Length]))
      .Where(candidate => directory == candidate.Directory
        || directory.StartsWith(candidate.Directory + "/", StringComparison.Ordinal))
      .OrderByDescending(candidate => candidate.Directory.Length)
      .FirstOrDefault();

    if (project.Name is null)
    {
      return null;
    }

    var folders = directory.Length > project.Directory.Length
      ? directory[(project.Directory.Length + 1)..].Split('/')
      : [];

    return string.Join('.', new[] { project.Name }.Concat(folders.Select(Naming.ToPascalCase)));
  }

  private static bool Matches(string actual, string expected) =>
    string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal);

  private static string Normalize(string name) =>
    name.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
