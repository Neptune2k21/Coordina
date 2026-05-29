namespace Coordina.Api.Modules.Projects.Contracts;

public sealed record CreateProjectRequest(
  string Name,
  string? Description,
  Guid? ProjectOwnerId = null,
  string? Key = null,
  string? Icon = null,
  string? Color = null);
