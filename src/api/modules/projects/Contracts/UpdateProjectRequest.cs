namespace Coordina.Api.Modules.Projects.Contracts;

public sealed record UpdateProjectRequest(
  string? Name = null,
  string? Description = null,
  Guid? ProjectOwnerId = null,
  string? Status = null,
  string? Key = null,
  string? Icon = null,
  string? Color = null);
