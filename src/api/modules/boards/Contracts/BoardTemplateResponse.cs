namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardTemplateResponse(
  string Id,
  string Name,
  string Summary,
  IReadOnlyCollection<BoardTemplateListResponse> Lists);

public sealed record BoardTemplateListResponse(
  string Title,
  string TaskStatus,
  IReadOnlyCollection<BoardTemplateCardResponse> Cards);

public sealed record BoardTemplateCardResponse(
  string Key,
  string Title,
  string Description,
  string Priority,
  int EstimatedMinutes,
  int? DueInDays,
  IReadOnlyCollection<string> Labels,
  IReadOnlyCollection<string> Subtasks,
  IReadOnlyCollection<string> DependsOn);
