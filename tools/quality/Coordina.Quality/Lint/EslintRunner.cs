using System.Diagnostics;
using System.Text.Json;
using Coordina.Quality.Checks;

namespace Coordina.Quality.Lint;

/// <summary>
/// Runs the web app's ESLint and reports its findings like any other rule, so
/// <c>make check</c> shows TypeScript problems next to the rest and CI turns
/// them into annotations. Rules are configured in <c>src/web/eslint.config.js</c>.
/// </summary>
internal sealed class EslintRunner(string repositoryRoot)
{
  public const string WebRoot = "src/web";
  public const string RelativeBinaryPath = "src/web/node_modules/.bin/eslint";
  private const string ConfigPath = "src/web/eslint.config.js";

  private static readonly string[] LintedExtensions = [".ts", ".tsx", ".js"];

  private static readonly string[] OutputArguments = ["--format", "json", "--no-warn-ignored"];

  private readonly Dictionary<string, RuleInfo> _rules = new(StringComparer.Ordinal);

  /// <summary>False outside a repository laid out like Coordina, where there is nothing to lint.</summary>
  public bool AppliesHere => File.Exists(Path.Combine(repositoryRoot, ConfigPath));

  public bool IsAvailable => File.Exists(Path.Combine(repositoryRoot, RelativeBinaryPath));

  public static bool IsLinted(string path) =>
    path.StartsWith(WebRoot + "/", StringComparison.Ordinal)
    && LintedExtensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal))
    && !RepositoryPaths.IsGenerated(path);

  /// <summary>Lints <paramref name="paths"/>, or the whole web app when it is <c>null</c>.</summary>
  public IReadOnlyList<Violation> Run(IReadOnlyCollection<string>? paths)
  {
    var targets = paths is null
      ? ["."]
      : paths.Where(IsLinted).Select(path => path[(WebRoot.Length + 1)..]).ToArray();

    if (targets.Length == 0)
    {
      return [];
    }

    var start = new ProcessStartInfo(Path.Combine(repositoryRoot, RelativeBinaryPath))
    {
      WorkingDirectory = Path.Combine(repositoryRoot, WebRoot),
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    foreach (var argument in OutputArguments.Concat(targets))
    {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)
      ?? throw new InvalidOperationException("Could not start ESLint.");
    var errors = process.StandardError.ReadToEndAsync();
    var json = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    // 0 means clean and 1 means findings; anything else is ESLint failing to run.
    if (process.ExitCode > 1)
    {
      throw new InvalidOperationException($"ESLint failed: {errors.Result}");
    }

    return Parse(json, repositoryRoot);
  }

  /// <summary>Reads ESLint's JSON output into violations with repository-relative paths.</summary>
  public IReadOnlyList<Violation> Parse(string json, string root)
  {
    using var document = JsonDocument.Parse(json);
    var violations = new List<Violation>();

    foreach (var file in document.RootElement.EnumerateArray())
    {
      var path = Path.GetRelativePath(root, file.GetProperty("filePath").GetString()!).Replace('\\', '/');

      foreach (var message in file.GetProperty("messages").EnumerateArray())
      {
        var isFatal = message.TryGetProperty("fatal", out var fatal) && fatal.GetBoolean();
        var ruleId = message.TryGetProperty("ruleId", out var rule) && rule.ValueKind == JsonValueKind.String
          ? rule.GetString()!
          : isFatal ? "parse-error" : null;

        if (ruleId is null)
        {
          continue;
        }

        var severity = isFatal || message.GetProperty("severity").GetInt32() == 2
          ? Severity.Error
          : Severity.Warning;
        var line = message.TryGetProperty("line", out var lineValue) ? lineValue.GetInt32() : 1;

        violations.Add(new Violation(RuleFor(ruleId, severity), path, line, message.GetProperty("message").GetString()!));
      }
    }

    return violations;
  }

  private RuleInfo RuleFor(string ruleId, Severity severity)
  {
    var key = $"{ruleId}|{severity}";

    if (!_rules.TryGetValue(key, out var info))
    {
      info = new RuleInfo(
        $"eslint/{ruleId}",
        $"ESLint rule {ruleId}",
        severity,
        "Configured in src/web/eslint.config.js. Run 'pnpm --dir src/web lint' for ESLint's own output.");
      _rules[key] = info;
    }

    return info;
  }
}
