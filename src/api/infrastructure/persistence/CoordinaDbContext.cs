using Coordina.Api.Modules.Auth.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Infrastructure.Persistence;

public sealed class CoordinaDbContext(DbContextOptions<CoordinaDbContext> options)
  : DbContext(options)
{
  public DbSet<AuthUserEntity> AuthUsers => Set<AuthUserEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoordinaDbContext).Assembly);
  }
}
