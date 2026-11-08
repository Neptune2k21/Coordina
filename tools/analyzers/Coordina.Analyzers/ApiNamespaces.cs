using System;

namespace Coordina.Analyzers;

/// <summary>The API's layers, as namespaces: modules → shared → core.</summary>
internal static class ApiNamespaces
{
  public const string Core = "Coordina.Api.Core";
  public const string Shared = "Coordina.Api.Shared";
  public const string Modules = "Coordina.Api.Modules";

  public static bool IsIn(string? name, string root) =>
    name is not null
    && (name == root || name.StartsWith(root + ".", StringComparison.Ordinal));

  /// <summary>The module a namespace belongs to, such as <c>Auth</c>, or <c>null</c>.</summary>
  public static string? ModuleOf(string? name)
  {
    if (name is null || !name.StartsWith(Modules + ".", StringComparison.Ordinal))
    {
      return null;
    }

    var rest = name.Substring(Modules.Length + 1);
    var end = rest.IndexOf('.');

    return end < 0 ? rest : rest.Substring(0, end);
  }

  /// <summary>True when one segment of <paramref name="name"/> is <paramref name="segment"/>.</summary>
  public static bool HasSegment(string? name, string segment) =>
    name is not null && Array.IndexOf(name.Split('.'), segment) >= 0;
}
