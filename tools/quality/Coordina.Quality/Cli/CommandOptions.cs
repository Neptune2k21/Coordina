namespace Coordina.Quality.Cli;

internal sealed record CommandOptions(
  bool All,
  bool Staged,
  bool DryRun,
  string? Base,
  string Format,
  string? Sarif,
  IReadOnlyList<string> Skip,
  IReadOnlyList<string> Positional)
{
  private static readonly string[] Formats = ["console", "github"];

  public static CommandOptions Parse(IEnumerable<string> arguments)
  {
    var queue = new Queue<string>(arguments);
    var options = new CommandOptions(false, false, false, null, "console", null, [], []);
    var skip = new List<string>();
    var positional = new List<string>();

    while (queue.Count > 0)
    {
      var argument = queue.Dequeue();

      options = argument switch
      {
        "--all" => options with { All = true },
        "--staged" => options with { Staged = true },
        "--dry-run" => options with { DryRun = true },
        "--base" => options with { Base = ValueOf(queue, argument) },
        "--format" => options with { Format = FormatOf(ValueOf(queue, argument)) },
        "--sarif" => options with { Sarif = ValueOf(queue, argument) },
        "--skip" => Add(options, skip, ValueOf(queue, argument)),
        _ when argument.StartsWith("--", StringComparison.Ordinal) =>
          throw new UsageException($"Unknown option '{argument}'."),
        _ => Add(options, positional, argument)
      };
    }

    if (options.All && options.Staged)
    {
      throw new UsageException("Use either --all or --staged, not both.");
    }

    return options with { Skip = skip, Positional = positional };
  }

  private static CommandOptions Add(CommandOptions options, List<string> values, string value)
  {
    values.Add(value);
    return options;
  }

  private static string ValueOf(Queue<string> queue, string option) =>
    queue.Count > 0 && !queue.Peek().StartsWith("--", StringComparison.Ordinal)
      ? queue.Dequeue()
      : throw new UsageException($"Option '{option}' needs a value.");

  private static string FormatOf(string value) =>
    Formats.Contains(value)
      ? value
      : throw new UsageException($"Unknown format '{value}'. Use {string.Join(" or ", Formats)}.");
}
