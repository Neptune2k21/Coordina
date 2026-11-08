using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardPlanningPreferenceEntityConfiguration
  : IEntityTypeConfiguration<BoardPlanningPreferenceEntity>
{
  public void Configure(EntityTypeBuilder<BoardPlanningPreferenceEntity> builder)
  {
    builder.ToTable("board_planning_preferences");

    builder.HasKey(preference => new { preference.BoardId, preference.UserId });

    builder.Property(preference => preference.BoardId)
      .HasColumnName("board_id");

    builder.Property(preference => preference.UserId)
      .HasColumnName("user_id");

    builder.Property(preference => preference.AvailableMinutes)
      .HasColumnName("available_minutes")
      .IsRequired();

    builder.Property(preference => preference.Context)
      .HasColumnName("context")
      .HasMaxLength(32);

    builder.Property(preference => preference.PreferAssigned)
      .HasColumnName("prefer_assigned")
      .IsRequired();

    builder.Property(preference => preference.IncludeUnassigned)
      .HasColumnName("include_unassigned")
      .IsRequired();

    builder.Property(preference => preference.UpdatedAt)
      .HasColumnName("updated_at")
      .IsRequired();

    builder.HasIndex(preference => preference.UserId);

    builder.HasOne<BoardEntity>()
      .WithMany()
      .HasForeignKey(preference => preference.BoardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
