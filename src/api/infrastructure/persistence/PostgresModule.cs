using Npgsql;

namespace Coordina.Api.Infrastructure.Persistence;

public static class PostgresModule
{
  public static IServiceCollection AddPostgres(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var options = PostgresOptions.FromEnvironment(configuration);

    services.AddSingleton(options);
    services.AddSingleton(_ => NpgsqlDataSource.Create(
      options.ToConnectionString()));

    return services;
  }
}
