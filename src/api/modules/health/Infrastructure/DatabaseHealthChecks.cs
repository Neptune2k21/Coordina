using Coordina.Api.Core.Persistence;

namespace Coordina.Api.Modules.Health.Infrastructure;

public static class DatabaseHealthChecks
{
  /// <summary>Unhealthy while PostgreSQL refuses connections.</summary>
  public static IHealthChecksBuilder AddDatabaseCheck(
    this IHealthChecksBuilder builder,
    params string[] tags) =>
    builder.AddDbContextCheck<CoordinaDbContext>("database", tags: tags);
}
