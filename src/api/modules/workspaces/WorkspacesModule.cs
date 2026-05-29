using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Infrastructure;

namespace Coordina.Api.Modules.Workspaces;

public static class WorkspacesModule
{
  public static IServiceCollection AddWorkspacesModule(
    this IServiceCollection services)
  {
    services.AddScoped<IWorkspaceStore, PostgresWorkspaceStore>();
    services.AddScoped<IWorkspaceService, WorkspaceService>();

    return services;
  }

  public static IServiceCollection AddInMemoryWorkspaceStoreForTests(
    this IServiceCollection services)
  {
    var workspaceStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(IWorkspaceStore));

    if (workspaceStoreDescriptor is not null)
    {
      services.Remove(workspaceStoreDescriptor);
    }

    services.AddSingleton<IWorkspaceStore, InMemoryWorkspaceStore>();

    return services;
  }
}
