using Coordina.Api.Modules.Tasks.Application;
using Coordina.Api.Modules.Tasks.Infrastructure;

namespace Coordina.Api.Modules.Tasks;

public static class TasksModule
{
  public static IServiceCollection AddTasksModule(
    this IServiceCollection services)
  {
    services.AddScoped<ITaskStore, PostgresTaskStore>();
    services.AddScoped<ITaskService, TaskService>();

    return services;
  }

  public static IServiceCollection AddInMemoryTaskStoreForTests(
    this IServiceCollection services)
  {
    var taskStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(ITaskStore));

    if (taskStoreDescriptor is not null)
    {
      services.Remove(taskStoreDescriptor);
    }

    services.AddSingleton<ITaskStore, InMemoryTaskStore>();

    return services;
  }
}
