using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardForecastSnapshotEntityConfiguration
  : IEntityTypeConfiguration<BoardForecastSnapshotEntity>
{
  public void Configure(EntityTypeBuilder<BoardForecastSnapshotEntity> builder)
  {
    builder.ToTable("board_forecast_snapshots");

    builder.HasKey(snapshot => new { snapshot.MilestoneId, snapshot.TakenOn });

    builder.Property(snapshot => snapshot.BoardId)
      .HasColumnName("board_id")
      .IsRequired();

    builder.Property(snapshot => snapshot.MilestoneId)
      .HasColumnName("milestone_id")
      .IsRequired();

    builder.Property(snapshot => snapshot.TakenOn)
      .HasColumnName("taken_on")
      .IsRequired();

    builder.Property(snapshot => snapshot.TargetDate)
      .HasColumnName("target_date")
      .IsRequired();

    builder.Property(snapshot => snapshot.ForecastDate)
      .HasColumnName("forecast_date")
      .IsRequired();

    builder.Property(snapshot => snapshot.CommitDate)
      .HasColumnName("commit_date")
      .IsRequired();

    builder.Property(snapshot => snapshot.OnTime)
      .HasColumnName("on_time")
      .IsRequired();

    builder.Property(snapshot => snapshot.OpenCardCount)
      .HasColumnName("open_card_count")
      .IsRequired();

    builder.HasIndex(snapshot => snapshot.BoardId);

    builder.HasOne(snapshot => snapshot.Board)
      .WithMany()
      .HasForeignKey(snapshot => snapshot.BoardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
