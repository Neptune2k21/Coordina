using Microsoft.EntityFrameworkCore;

namespace Coordina.Api.Core.Persistence;

public static class PostgresServiceCollectionExtensions
{
  public static IServiceCollection AddPostgres(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddSingleton(PostgresOptions.FromEnvironment(configuration));
    services.AddDbContext<CoordinaDbContext>((provider, dbContextOptions) =>
      dbContextOptions.UseNpgsql(
        provider.GetRequiredService<PostgresOptions>().ToConnectionString()));

    return services;
  }
}
