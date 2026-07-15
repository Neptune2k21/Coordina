using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardCardSubtaskEntityConfiguration
  : IEntityTypeConfiguration<BoardCardSubtaskEntity>
{
  public void Configure(EntityTypeBuilder<BoardCardSubtaskEntity> builder)
  {
    builder.ToTable("board_card_subtasks");

    builder.HasKey(subtask => subtask.Id);

    builder.Property(subtask => subtask.Id)
      .HasColumnName("id");

    builder.Property(subtask => subtask.CardId)
      .HasColumnName("card_id")
      .IsRequired();

    builder.Property(subtask => subtask.Title)
      .HasColumnName("title")
      .HasMaxLength(180)
      .IsRequired();

    builder.Property(subtask => subtask.IsCompleted)
      .HasColumnName("is_completed")
      .HasDefaultValue(false)
      .IsRequired();

    builder.Property(subtask => subtask.CompletedAt)
      .HasColumnName("completed_at");

    builder.Property(subtask => subtask.CompletedByUserId)
      .HasColumnName("completed_by_user_id");

    builder.Property(subtask => subtask.Position)
      .HasColumnName("position")
      .IsRequired();

    builder.Property(subtask => subtask.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.Property(subtask => subtask.UpdatedAt)
      .HasColumnName("updated_at")
      .IsRequired();

    builder.HasIndex(subtask => new { subtask.CardId, subtask.Position });
    builder.HasIndex(subtask => subtask.CompletedByUserId);

    builder.HasOne(subtask => subtask.Card)
      .WithMany(card => card.Subtasks)
      .HasForeignKey(subtask => subtask.CardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
