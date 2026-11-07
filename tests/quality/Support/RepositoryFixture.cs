namespace Coordina.Quality.Tests.Support;

internal static class RepositoryFixture
{
  /// <summary>The Coordina repository these tests run from.</summary>
  public static string Root { get; } = FindRoot();

  /// <summary>
  /// Links the web app's installed dependencies into <paramref name="directory"/>,
  /// so tools such as jscpd run in a throwaway repository as they do here.
  /// </summary>
  public static void LinkWebDependencies(string directory)
  {
    var source = Path.Combine(Root, "src", "web", "node_modules");
    var target = Path.Combine(directory, "src", "web", "node_modules");

    Assert.True(
      Directory.Exists(source),
      "The duplication tests need the web dependencies. Run 'pnpm --dir src/web install'.");

    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    Directory.CreateSymbolicLink(target, source);
  }

  private static string FindRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Coordina.sln")))
    {
      directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Coordina.sln not found.");
  }
}
