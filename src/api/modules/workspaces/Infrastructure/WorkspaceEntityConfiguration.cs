using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordina.Api.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceEntityConfiguration
  : IEntityTypeConfiguration<WorkspaceEntity>
{
  public void Configure(EntityTypeBuilder<WorkspaceEntity> builder)
  {
    builder.ToTable("workspaces");

    builder.HasKey(workspace => workspace.Id);

    builder.Property(workspace => workspace.Id)
      .HasColumnName("id");

    builder.Property(workspace => workspace.Name)
      .HasColumnName("name")
      .HasMaxLength(80)
      .IsRequired();

    builder.Property(workspace => workspace.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.HasMany(workspace => workspace.Members)
      .WithOne(member => member.Workspace)
      .HasForeignKey(member => member.WorkspaceId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasMany(workspace => workspace.Projects)
      .WithOne(project => project.Workspace)
      .HasForeignKey(project => project.WorkspaceId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
