using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Boards.Infrastructure;

public sealed class BoardCardDependencyEntityConfiguration
  : IEntityTypeConfiguration<BoardCardDependencyEntity>
{
  public void Configure(EntityTypeBuilder<BoardCardDependencyEntity> builder)
  {
    builder.ToTable("board_card_dependencies");

    builder.HasKey(dependency => dependency.Id);

    builder.Property(dependency => dependency.Id)
      .HasColumnName("id");

    builder.Property(dependency => dependency.CardId)
      .HasColumnName("card_id")
      .IsRequired();

    builder.Property(dependency => dependency.DependsOnCardId)
      .HasColumnName("depends_on_card_id")
      .IsRequired();

    builder.Property(dependency => dependency.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.HasIndex(dependency => new
    {
      dependency.CardId,
      dependency.DependsOnCardId
    }).IsUnique();
    builder.HasIndex(dependency => dependency.DependsOnCardId);

    builder.HasOne(dependency => dependency.Card)
      .WithMany(card => card.Dependencies)
      .HasForeignKey(dependency => dependency.CardId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
