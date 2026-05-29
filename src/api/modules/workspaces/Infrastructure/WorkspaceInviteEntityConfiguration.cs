using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceInviteEntityConfiguration
  : IEntityTypeConfiguration<WorkspaceInviteEntity>
{
  public void Configure(EntityTypeBuilder<WorkspaceInviteEntity> builder)
  {
    builder.ToTable("workspace_invites");

    builder.HasKey(invite => invite.Id);

    builder.Property(invite => invite.Id)
      .HasColumnName("id");

    builder.Property(invite => invite.WorkspaceId)
      .HasColumnName("workspace_id")
      .IsRequired();

    builder.Property(invite => invite.CreatedByUserId)
      .HasColumnName("created_by_user_id")
      .IsRequired();

    builder.Property(invite => invite.ConsumedByUserId)
      .HasColumnName("consumed_by_user_id");

    builder.Property(invite => invite.CodeHash)
      .HasColumnName("code_hash")
      .HasMaxLength(64)
      .IsRequired();

    builder.Property(invite => invite.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.Property(invite => invite.ExpiresAt)
      .HasColumnName("expires_at")
      .IsRequired();

    builder.Property(invite => invite.ConsumedAt)
      .HasColumnName("consumed_at");

    builder.HasIndex(invite => invite.CodeHash)
      .IsUnique();

    builder.HasIndex(invite => invite.WorkspaceId);

    builder.HasOne(invite => invite.Workspace)
      .WithMany(workspace => workspace.Invites)
      .HasForeignKey(invite => invite.WorkspaceId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
