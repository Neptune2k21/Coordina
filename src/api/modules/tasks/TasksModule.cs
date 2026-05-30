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
    services.AddScoped<IBoardStore, PostgresBoardStore>();
    services.AddScoped<IBoardService, BoardService>();

    return services;
  }

  public static IServiceCollection AddInMemoryTaskStoreForTests(
    this IServiceCollection services)
  {
    var taskStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(ITaskStore));
    var boardStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(IBoardStore));

    if (taskStoreDescriptor is not null)
    {
      services.Remove(taskStoreDescriptor);
    }

    if (boardStoreDescriptor is not null)
    {
      services.Remove(boardStoreDescriptor);
    }

    services.AddSingleton<ITaskStore, InMemoryTaskStore>();
    services.AddSingleton<IBoardStore, InMemoryBoardStore>();

    return services;
  }
}
