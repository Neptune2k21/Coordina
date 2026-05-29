using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Auth.Infrastructure;

public sealed class AuthUserEntityConfiguration : IEntityTypeConfiguration<AuthUserEntity>
{
  public void Configure(EntityTypeBuilder<AuthUserEntity> builder)
  {
    builder.ToTable("auth_users");

    builder.HasKey(user => user.Id);

    builder.Property(user => user.Id)
      .HasColumnName("id");

    builder.Property(user => user.Name)
      .HasColumnName("name")
      .IsRequired();

    builder.Property(user => user.Email)
      .HasColumnName("email")
      .IsRequired();

    builder.Property(user => user.NormalizedEmail)
      .HasColumnName("normalized_email")
      .IsRequired();

    builder.HasIndex(user => user.NormalizedEmail)
      .IsUnique();

    builder.Property(user => user.PasswordHash)
      .HasColumnName("password_hash")
      .IsRequired();

    builder.Property(user => user.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();
  }
}
