using System.Text.RegularExpressions;

namespace Coordina.Quality.Checks;

internal static partial class Naming
{
  public static bool IsPascalCase(string name) => PascalCasePattern().IsMatch(name);

  public static bool IsCamelCase(string name) => CamelCasePattern().IsMatch(name);

  public static bool IsPrivateFieldName(string name) => PrivateFieldPattern().IsMatch(name);

  /// <summary>xUnit names such as <c>CreateBoard_WithTemplate_ReturnsLists</c>.</summary>
  public static bool IsTestMethodName(string name) => TestMethodPattern().IsMatch(name);

  public static bool IsKebabCase(string name) => KebabCasePattern().IsMatch(name);

  public static bool IsSnakeCase(string name) => SnakeCasePattern().IsMatch(name);

  public static bool IsDiscard(string name) => name.Trim('_').Length == 0;

  /// <summary>Turns <c>api-postgres</c>, <c>graph_engine</c> or <c>boards</c> into PascalCase.</summary>
  public static string ToPascalCase(string name) =>
    string.Concat(name
      .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries)
      .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

  public static string WithoutVerbatimPrefix(string name) => name.TrimStart('@');

  [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
  private static partial Regex PascalCasePattern();

  [GeneratedRegex("^[a-z][A-Za-z0-9]*$")]
  private static partial Regex CamelCasePattern();

  [GeneratedRegex("^_[a-z][A-Za-z0-9]*$")]
  private static partial Regex PrivateFieldPattern();

  [GeneratedRegex("^[A-Z][A-Za-z0-9]*(_[A-Z0-9][A-Za-z0-9]*)*$")]
  private static partial Regex TestMethodPattern();

  [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
  private static partial Regex KebabCasePattern();

  [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
  private static partial Regex SnakeCasePattern();
}
