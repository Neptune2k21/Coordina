using System.Text.RegularExpressions;
using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Boards.Application;

public sealed partial record BoardPlanningPreferences(
  Guid UserId,
  CardSize? MaxSize,
  string? Context,
  bool PreferAssigned,
  bool IncludeUnassigned)
{
  private const int MaxContextLength = 32;

  public static Result<BoardPlanningPreferences> Parse(
    BoardPlanningRequest request,
    Guid userId)
  {
    var context = NormalizeContext(request.Context);
    var errors = new ValidationErrors()
      .Code("MaxSize", request.MaxSize, "Size limit", out CardSize? maxSize)
      .AddIf(
        context?.Length > MaxContextLength,
        "Context",
        $"Context must be {MaxContextLength} characters or fewer.");

    return errors.HasErrors
      ? errors.ToError()
      : Result.Success(new BoardPlanningPreferences(
        userId,
        maxSize,
        context,
        request.PreferAssigned ?? true,
        request.IncludeUnassigned ?? true));
  }

  public bool MatchesContext(IEnumerable<string> labels) =>
    Context is not null
    && labels.Any(label => string.Equals(
      NormalizeContext(label),
      Context,
      StringComparison.OrdinalIgnoreCase));

  private static string? NormalizeContext(string? value)
  {
    var normalized = WhitespacePattern()
      .Replace(value?.Trim().TrimStart('#') ?? string.Empty, "-");

    return normalized.Length == 0 ? null : normalized;
  }

  [GeneratedRegex(@"\s+")]
  private static partial Regex WhitespacePattern();
}
