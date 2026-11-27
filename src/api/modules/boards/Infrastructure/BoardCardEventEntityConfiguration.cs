using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Boards.Infrastructure;

/// <summary>
/// The rows are written by triggers (see the AddBoardHistory migration), so
/// that no write can skip them. An event outlives its card, not its board.
/// </summary>
public sealed class BoardCardEventEntityConfiguration
  : IEntityTypeConfiguration<BoardCardEventEntity>
{
  public void Configure(EntityTypeBuilder<BoardCardEventEntity> builder)
  {
    builder.ToTable("board_card_events");

    builder.HasKey(cardEvent => cardEvent.Id);

    builder.Property(cardEvent => cardEvent.Id)
      .HasColumnName("id")
      .UseIdentityAlwaysColumn();

    builder.Property(cardEvent => cardEvent.BoardId)
      .HasColumnName("board_id")
      .IsRequired();

    builder.Property(cardEvent => cardEvent.CardId)
      .HasColumnName("card_id")
      .IsRequired();

    builder.Property(cardEvent => cardEvent.Kind)
      .HasColumnName("kind")
      .HasConversion(
        kind => EnumCode.Format(kind),
        value => EnumCode.Parse<BoardCardEventKind>(value))
      .HasMaxLength(24)
      .IsRequired();

    builder.Property(cardEvent => cardEvent.FromValue)
      .HasColumnName("from_value")
      .HasMaxLength(64);

    builder.Property(cardEvent => cardEvent.ToValue)
      .HasColumnName("to_value")
      .HasMaxLength(64);

    builder.Property(cardEvent => cardEvent.OccurredAt)
      .HasColumnName("occurred_at")
      .IsRequired();

    builder.HasIndex(cardEvent => new { cardEvent.BoardId, cardEvent.Id });

    builder.HasOne(cardEvent => cardEvent.Board)
      .WithMany()
      .HasForeignKey(cardEvent => cardEvent.BoardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
