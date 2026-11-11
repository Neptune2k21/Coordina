using Coordina.Api.Core.Hosting;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Infrastructure;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Infrastructure;
using Coordina.Api.Modules.Projects.Application;
using Coordina.Api.Modules.Projects.Infrastructure;
using Coordina.Api.Modules.Workspaces.Application;
using Coordina.Api.Modules.Workspaces.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coordina.Api.Tests.Support;

public sealed class ApiTestApplicationFactory : WebApplicationFactory<Program>
{
  /// <summary>The web app origin the test API allows, as a deployment would configure it.</summary>
  public const string WebAppOrigin = "https://app.coordina.test";

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment(CoreHostingExtensions.TestingEnvironment);
    builder.UseSetting("Cors:AllowedOrigins:0", WebAppOrigin);
    builder.ConfigureTestServices(services =>
    {
      services.Replace(ServiceDescriptor.Singleton<IUserStore, InMemoryUserStore>());
      services.Replace(ServiceDescriptor.Singleton<IRefreshTokenStore, InMemoryRefreshTokenStore>());
      services.Replace(ServiceDescriptor.Singleton<IWorkspaceStore, InMemoryWorkspaceStore>());
      services.Replace(ServiceDescriptor.Singleton<IProjectStore, InMemoryProjectStore>());
      services.Replace(ServiceDescriptor.Singleton<IBoardStore, InMemoryBoardStore>());
      services.Replace(ServiceDescriptor.Singleton<IBoardPlanningPreferenceStore, InMemoryBoardPlanningPreferenceStore>());
    });
  }
}
