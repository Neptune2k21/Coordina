using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardPlanningPreferenceEntityConfiguration
  : IEntityTypeConfiguration<BoardPlanningPreferenceEntity>
{
  public void Configure(EntityTypeBuilder<BoardPlanningPreferenceEntity> builder)
  {
    builder.ToTable("board_planning_preferences", table => table.HasCheckConstraint(
      "ck_board_planning_preferences_max_size",
      "max_size IS NULL OR max_size IN ('XS', 'S', 'M', 'L', 'XL', 'XXL')"));

    builder.HasKey(preference => new { preference.BoardId, preference.UserId });

    builder.Property(preference => preference.BoardId)
      .HasColumnName("board_id");

    builder.Property(preference => preference.UserId)
      .HasColumnName("user_id");

    builder.Property(preference => preference.MaxSize)
      .HasColumnName("max_size")
      .HasConversion(
        size => size == null
          ? null
          : EnumCode.Format(size.Value),
        value => string.IsNullOrWhiteSpace(value)
          ? null
          : EnumCode.Parse<CardSize>(value))
      .HasMaxLength(4);

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
