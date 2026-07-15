using Coordina.Api.Modules.Projects.Infrastructure;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardEntity
{
  public Guid Id { get; set; }
  public Guid ProjectId { get; set; }
  public Guid WorkspaceId { get; set; }
  public required string Name { get; set; }
  public BoardTemplate Template { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }

  public ProjectEntity? Project { get; set; }
  public ICollection<BoardListEntity> Lists { get; set; } = [];
}
