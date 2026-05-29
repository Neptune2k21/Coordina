using Coordina.Api.Modules.Auth.Infrastructure;
using Coordina.Api.Modules.Projects.Infrastructure;
using Coordina.Api.Modules.Workspaces.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Infrastructure.Persistence;

public sealed class CoordinaDbContext(DbContextOptions<CoordinaDbContext> options)
  : DbContext(options)
{
  public DbSet<AuthUserEntity> AuthUsers => Set<AuthUserEntity>();
  public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();
  public DbSet<WorkspaceMemberEntity> WorkspaceMembers => Set<WorkspaceMemberEntity>();
  public DbSet<WorkspaceInviteEntity> WorkspaceInvites => Set<WorkspaceInviteEntity>();
  public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoordinaDbContext).Assembly);
  }
}
