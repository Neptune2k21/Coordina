using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
{
  public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
  {
    builder.ToTable("auth_refresh_tokens");

    builder.HasKey(token => token.Id);

    builder.Property(token => token.Id)
      .HasColumnName("id");

    builder.Property(token => token.UserId)
      .HasColumnName("user_id")
      .IsRequired();

    builder.Property(token => token.FamilyId)
      .HasColumnName("family_id")
      .IsRequired();

    builder.Property(token => token.TokenHash)
      .HasColumnName("token_hash")
      .HasMaxLength(64)
      .IsRequired();

    builder.Property(token => token.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.Property(token => token.ExpiresAt)
      .HasColumnName("expires_at")
      .IsRequired();

    builder.Property(token => token.RevokedAt)
      .HasColumnName("revoked_at");

    builder.Property(token => token.ReplacedById)
      .HasColumnName("replaced_by_id");

    builder.HasIndex(token => token.TokenHash)
      .IsUnique();

    builder.HasIndex(token => token.FamilyId);

    builder.HasOne<AuthUserEntity>()
      .WithMany()
      .HasForeignKey(token => token.UserId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
