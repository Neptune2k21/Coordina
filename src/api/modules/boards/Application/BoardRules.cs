using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;
using Coordina.Api.Shared.Text;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Boards.Application;

internal static class BoardRules
{
  public const int MinEstimatedMinutes = 5;
  public const int MaxEstimatedMinutes = 2880;
  private const int MaxLabels = 12;
  private const int MaxCustomLists = 12;
  private const int MaxListTitleLength = 80;
  private const int MaxCardTitleLength = 180;

  public static BoardTemplate? ParseTemplate(
    string? template,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(template))
    {
      error = "Board template is required.";
      return null;
    }

    if (EnumCode.TryParse<BoardTemplate>(template, out var parsed))
    {
      return parsed;
    }

    error = "Board template must be BASIC, AGILE_SCRUM, BUG_TRACKING, PRODUCT_ROADMAP, or CUSTOM.";
    return null;
  }

  public static BoardCardPriority? ParsePriority(
    string? priority,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(priority))
    {
      return null;
    }

    if (EnumCode.TryParse<BoardCardPriority>(priority, out var parsed))
    {
      return parsed;
    }

    error = "Card priority must be LOW, MEDIUM, or HIGH.";
    return null;
  }

  public static BoardListSeed[] BuildListSeeds(
    BoardTemplate template,
    string[]? customListTitles)
  {
    if (template == BoardTemplate.Custom)
    {
      var titles = TextNormalizer.DistinctNonBlank(customListTitles, MaxCustomLists);

      return titles
        .Select((title, index) => new BoardListSeed(
          title,
          CustomListStatus(index, titles.Length),
          []))
        .ToArray();
    }

    return BoardTemplateCatalog.GetLists(template)
      .Select(list => new BoardListSeed(
        list.Title,
        list.TaskStatus,
        list.Cards
          .Select(card => new BoardCardSeed(
            card.Title,
            card.Description,
            card.Priority,
            card.Labels))
          .ToArray()))
      .ToArray();
  }

  public static BoardTaskStatus ParseListStatus(
    string? taskStatus,
    out string? error)
  {
    error = null;

    if (string.IsNullOrWhiteSpace(taskStatus))
    {
      return BoardTaskStatus.Todo;
    }

    if (EnumCode.TryParse<BoardTaskStatus>(taskStatus, out var parsed))
    {
      return parsed;
    }

    error = "List task status must be TODO, IN_PROGRESS, or DONE.";
    return BoardTaskStatus.Todo;
  }

  private static BoardTaskStatus CustomListStatus(int index, int count)
  {
    if (index == 0)
    {
      return BoardTaskStatus.Todo;
    }

    return index == count - 1
      ? BoardTaskStatus.Done
      : BoardTaskStatus.InProgress;
  }

  public static ValidationErrors ValidateBoard(
    string? name,
    BoardTemplate? template,
    string? templateError,
    BoardListSeed[] listSeeds)
  {
    var errors = new ValidationErrors()
      .Required("Name", name, "Board name", maxLength: 120);

    if (templateError is not null)
    {
      errors.Add("Template", templateError);
    }

    if (template == BoardTemplate.Custom && listSeeds.Length == 0)
    {
      errors.Add("CustomListTitles", "Custom boards need at least one list title.");
    }
    else if (template == BoardTemplate.Custom
      && listSeeds.Any(list => list.Title.Length > MaxListTitleLength))
    {
      errors.Add(
        "CustomListTitles",
        $"Custom list titles must be {MaxListTitleLength} characters or fewer.");
    }

    return errors;
  }

  public static ValidationErrors ValidateList(string? title) =>
    new ValidationErrors()
      .Required("Title", title, "List title", maxLength: MaxListTitleLength);

  public static ValidationErrors ValidateCardTitle(string? title) =>
    new ValidationErrors()
      .Required("Title", title, "Card title", maxLength: MaxCardTitleLength);

  public static ValidationErrors ValidateCard(
    string? title,
    string? description,
    string? priorityError,
    IReadOnlyCollection<string> labels,
    DateOnly? dueDate,
    int? estimatedMinutes,
    DateOnly today)
  {
    var errors = ValidateCardTitle(title)
      .MaxLength("Description", description, "Card description", 2000)
      .AddIf(
        labels.Any(label => label.Length > 32),
        "Labels",
        "Labels must be 32 characters or fewer.")
      .AddIf(
        dueDate < today,
        "DueDate",
        "Due date cannot be in the past.")
      .AddIf(
        estimatedMinutes is < MinEstimatedMinutes or > MaxEstimatedMinutes,
        "EstimatedMinutes",
        $"Effort must be between {MinEstimatedMinutes} and {MaxEstimatedMinutes} minutes.");

    if (priorityError is not null)
    {
      errors.Add("Priority", priorityError);
    }

    return errors;
  }

  public static string[] NormalizeLabels(string[]? labels) =>
    TextNormalizer.DistinctNonBlank(labels, MaxLabels);
}
