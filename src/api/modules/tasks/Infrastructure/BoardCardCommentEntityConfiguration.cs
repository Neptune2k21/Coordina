using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class BoardCardCommentEntityConfiguration
  : IEntityTypeConfiguration<BoardCardCommentEntity>
{
  public void Configure(EntityTypeBuilder<BoardCardCommentEntity> builder)
  {
    builder.ToTable("board_card_comments");

    builder.HasKey(comment => comment.Id);

    builder.Property(comment => comment.Id)
      .HasColumnName("id");

    builder.Property(comment => comment.CardId)
      .HasColumnName("card_id")
      .IsRequired();

    builder.Property(comment => comment.UserId)
      .HasColumnName("user_id")
      .IsRequired();

    builder.Property(comment => comment.Body)
      .HasColumnName("body")
      .HasMaxLength(1000)
      .IsRequired();

    builder.Property(comment => comment.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.HasIndex(comment => comment.CardId);
    builder.HasIndex(comment => comment.UserId);
    builder.HasIndex(comment => new { comment.CardId, comment.CreatedAt });

    builder.HasOne(comment => comment.Card)
      .WithMany(card => card.Comments)
      .HasForeignKey(comment => comment.CardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
