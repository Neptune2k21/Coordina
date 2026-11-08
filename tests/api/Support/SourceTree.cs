namespace Coordina.Api.Tests.Support;

/// <summary>Finds the repository, for tests that read committed files such as contracts.</summary>
internal static class SourceTree
{
  private static readonly Lazy<string> RepositoryRootPath = new(FindRepositoryRoot);

  public static string RepositoryRoot => RepositoryRootPath.Value;

  private static string FindRepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null
      && !File.Exists(Path.Combine(directory.FullName, "Coordina.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory.FullName;
  }
}
