namespace Coordina.Quality.Lint;

/// <summary>
/// The clang-format and clang-tidy that <c>make cpp-tools</c> installs, at the
/// versions pinned in <c>tools/clang/requirements.txt</c>. Each release formats
/// and analyses slightly differently, so the checks never fall back to whatever
/// version happens to be on the machine: a laptop and CI give the same answer.
/// </summary>
internal sealed class ClangTools(string repositoryRoot)
{
  public const string RequirementsPath = "tools/clang/requirements.txt";
  public const string FormatBinaryPath = InstallDirectory + "/clang_format/data/bin/clang-format";
  public const string TidyBinaryPath = InstallDirectory + "/clang_tidy/data/bin/clang-tidy";

  private const string InstallDirectory = ".tools/clang";

  // make cpp-tools copies the requirements it installed here.
  private const string InstalledRequirementsPath = InstallDirectory + "/requirements.txt";

  private static readonly string[] ConfigurationFiles =
  [
    ".clang-format",
    ".clang-tidy",
    "compile_flags.txt",
    RequirementsPath
  ];

  /// <summary>False outside a repository laid out like Coordina, where there is no C++ to check.</summary>
  public bool AppliesHere => File.Exists(Path.Combine(repositoryRoot, RequirementsPath));

  /// <summary>Both tools are installed, at the pinned versions.</summary>
  public bool IsInstalled =>
    File.Exists(Path.Combine(repositoryRoot, FormatBinaryPath))
    && File.Exists(Path.Combine(repositoryRoot, TidyBinaryPath))
    && File.Exists(Path.Combine(repositoryRoot, InstalledRequirementsPath))
    && File.ReadAllText(Path.Combine(repositoryRoot, InstalledRequirementsPath))
      == File.ReadAllText(Path.Combine(repositoryRoot, RequirementsPath));

  public string Format => Path.Combine(repositoryRoot, FormatBinaryPath);

  public string Tidy => Path.Combine(repositoryRoot, TidyBinaryPath);

  /// <summary>
  /// A file that changes what the tools report for every C++ file, so changing
  /// it checks all of them, not only the files it sits next to.
  /// </summary>
  public static bool IsConfiguration(string path) =>
    ConfigurationFiles.Contains(path)
    || path.EndsWith("/.clang-tidy", StringComparison.Ordinal)
    || path.EndsWith("/.clang-format", StringComparison.Ordinal);
}
