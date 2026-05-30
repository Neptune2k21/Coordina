using Coordina.Api.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskStatus = Coordina.Api.Modules.Tasks.Domain.TaskStatus;

namespace Coordina.Api.Modules.Tasks.Infrastructure;

public sealed class TaskEntityConfiguration
  : IEntityTypeConfiguration<TaskEntity>
{
  public void Configure(EntityTypeBuilder<TaskEntity> builder)
  {
    builder.ToTable("tasks");

    builder.HasKey(task => task.Id);

    builder.Property(task => task.Id)
      .HasColumnName("id");

    builder.Property(task => task.ProjectId)
      .HasColumnName("project_id")
      .IsRequired();

    builder.Property(task => task.WorkspaceId)
      .HasColumnName("workspace_id")
      .IsRequired();

    builder.Property(task => task.Title)
      .HasColumnName("title")
      .HasMaxLength(160)
      .IsRequired();

    builder.Property(task => task.Description)
      .HasColumnName("description")
      .HasMaxLength(1000);

    builder.Property(task => task.Status)
      .HasColumnName("status")
      .HasConversion(
        status => ToApiStatus(status),
        value => ParseStatus(value))
      .HasMaxLength(24)
      .IsRequired();

    builder.Property(task => task.Priority)
      .HasColumnName("priority")
      .HasConversion(
        priority => priority == null
          ? null
          : priority.Value.ToString().ToUpperInvariant(),
        value => string.IsNullOrWhiteSpace(value)
          ? null
          : Enum.Parse<TaskPriority>(value, true))
      .HasMaxLength(12);

    builder.Property(task => task.CreatedAt)
      .HasColumnName("created_at")
      .IsRequired();

    builder.Property(task => task.UpdatedAt)
      .HasColumnName("updated_at")
      .IsRequired();

    builder.HasIndex(task => new { task.WorkspaceId, task.ProjectId });
    builder.HasIndex(task => new { task.WorkspaceId, task.ProjectId, task.Status });
    builder.HasIndex(task => task.ProjectId);

    builder.HasOne(task => task.Project)
      .WithMany()
      .HasForeignKey(task => task.ProjectId)
      .OnDelete(DeleteBehavior.Cascade);
  }

  private static string ToApiStatus(TaskStatus status) =>
    status == TaskStatus.InProgress
      ? "IN_PROGRESS"
      : status.ToString().ToUpperInvariant();

  private static TaskStatus ParseStatus(string value)
  {
    var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
    return Enum.Parse<TaskStatus>(normalized, true);
  }
}
