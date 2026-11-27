namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardForecastSnapshotEntity
{
  public Guid BoardId { get; set; }
  public Guid MilestoneId { get; set; }
  public DateOnly TakenOn { get; set; }
  public DateOnly TargetDate { get; set; }
  public DateOnly ForecastDate { get; set; }
  public DateOnly CommitDate { get; set; }
  public double OnTime { get; set; }
  public int OpenCardCount { get; set; }

  public BoardEntity? Board { get; set; }
}
