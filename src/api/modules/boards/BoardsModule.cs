using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Infrastructure;

namespace Coordina.Api.Modules.Boards;

public static class BoardsModule
{
  public static IServiceCollection AddBoardsModule(
    this IServiceCollection services)
  {
    services.AddScoped<IBoardStore, PostgresBoardStore>();
    services.AddScoped<IBoardCatalogService, BoardCatalogService>();
    services.AddScoped<IBoardListService, BoardListService>();
    services.AddScoped<IBoardCardService, BoardCardService>();
    services.AddScoped<IBoardCollaborationService, BoardCollaborationService>();
    services.AddScoped<IBoardDependencyService, BoardDependencyService>();
    services.AddSingleton<IGraphEngine<Guid>, NativeGuidGraphEngine>();

    return services;
  }

  public static IServiceCollection AddInMemoryBoardStoreForTests(
    this IServiceCollection services)
  {
    var boardStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(IBoardStore));

    if (boardStoreDescriptor is not null)
    {
      services.Remove(boardStoreDescriptor);
    }

    services.AddSingleton<IBoardStore, InMemoryBoardStore>();

    return services;
  }
}
