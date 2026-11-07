using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Boards.Contracts;

public sealed record BoardTemplateResponse(
  [property: EnumCodes<BoardTemplate>] string Id,
  string Name,
  string Summary,
  IReadOnlyCollection<BoardTemplateListResponse> Lists);

public sealed record BoardTemplateListResponse(
  string Title,
  [property: EnumCodes<BoardTaskStatus>] string TaskStatus,
  IReadOnlyCollection<BoardTemplateCardResponse> Cards);

public sealed record BoardTemplateCardResponse(
  string Key,
  string Title,
  string Description,
  [property: EnumCodes<BoardCardPriority>] string Priority,
  int EstimatedMinutes,
  int? DueInDays,
  IReadOnlyCollection<string> Labels,
  IReadOnlyCollection<string> Subtasks,
  IReadOnlyCollection<string> DependsOn);
