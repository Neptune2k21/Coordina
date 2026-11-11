using Coordina.Quality.Checks;
using Coordina.Quality.Commits;
using Coordina.Quality.Duplication;
using Coordina.Quality.Lint;
using Coordina.Quality.Rules.CSharp;
using Coordina.Quality.Rules.Files;

namespace Coordina.Quality.Rules;

/// <summary>Every rule, in the order they are listed to contributors.</summary>
internal static class RuleCatalog
{
  public static IReadOnlyList<Rule> Create(IReadOnlyCollection<string> projectFiles) =>
  [
    new PascalCaseRule(),
    new InterfacePrefixRule(),
    new AsyncSuffixRule(),
    new PrivateFieldRule(),
    new CamelCaseLocalsRule(),
    new DescriptiveNamesRule(),
    new FileMatchesTypeRule(),
    new NamespaceMatchesFolderRule(projectFiles),
    new ModuleLayoutRule(),
    new TypePlacementRule(),
    new FileLengthRule(),
    new MethodLengthRule(),
    new WebFileNamingRule(),
    new GraphEngineFileNamingRule(),
    new NolintReasonRule()
  ];

  /// <summary>Rules that are not file rules, listed by <c>quality rules</c> too.</summary>
  public static IReadOnlyList<RuleInfo> Others { get; } =
  [
    Suppressions.Info,
    DuplicationRule.Info,
    ClangFormatRunner.Info,
    .. CommitMessageChecker.Rules
  ];
}
