using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class BoardCardAssigneeEntityConfiguration
  : IEntityTypeConfiguration<BoardCardAssigneeEntity>
{
  public void Configure(EntityTypeBuilder<BoardCardAssigneeEntity> builder)
  {
    builder.ToTable("board_card_assignees");

    builder.HasKey(assignee => assignee.Id);

    builder.Property(assignee => assignee.Id)
      .HasColumnName("id");

    builder.Property(assignee => assignee.CardId)
      .HasColumnName("card_id")
      .IsRequired();

    builder.Property(assignee => assignee.UserId)
      .HasColumnName("user_id")
      .IsRequired();

    builder.Property(assignee => assignee.AssignedAt)
      .HasColumnName("assigned_at")
      .IsRequired();

    builder.HasIndex(assignee => new { assignee.CardId, assignee.UserId })
      .IsUnique();

    builder.HasIndex(assignee => assignee.UserId);

    builder.HasOne(assignee => assignee.Card)
      .WithMany(card => card.Assignees)
      .HasForeignKey(assignee => assignee.CardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
