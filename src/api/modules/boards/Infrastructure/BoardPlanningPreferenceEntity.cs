namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardPlanningPreferenceEntity
{
  public Guid BoardId { get; set; }
  public Guid UserId { get; set; }
  public int AvailableMinutes { get; set; }
  public string? Context { get; set; }
  public bool PreferAssigned { get; set; }
  public bool IncludeUnassigned { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }
}
