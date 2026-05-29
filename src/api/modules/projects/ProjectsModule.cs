using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Infrastructure;

namespace Coordina.Api.Modules.Projects;

public static class ProjectsModule
{
  public static IServiceCollection AddProjectsModule(
    this IServiceCollection services)
  {
    services.AddScoped<IProjectStore, PostgresProjectStore>();
    services.AddScoped<IProjectService, ProjectService>();

    return services;
  }

  public static IServiceCollection AddInMemoryProjectStoreForTests(
    this IServiceCollection services)
  {
    var projectStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(IProjectStore));

    if (projectStoreDescriptor is not null)
    {
      services.Remove(projectStoreDescriptor);
    }

    services.AddSingleton<IProjectStore, InMemoryProjectStore>();

    return services;
  }
}
