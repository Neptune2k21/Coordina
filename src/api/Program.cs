using Coordina.Api.Core.Configuration;
using Coordina.Api.Core.Hosting;
using Coordina.Api.Core.Modules;
using Coordina.Api.Modules.Auth;
using Coordina.Api.Modules.Boards;
using Coordina.Api.Modules.Health;
using Coordina.Api.Modules.Projects;
using Coordina.Api.Modules.Work;
using Coordina.Api.Modules.Workspaces;
using Coordina.Api.Shared.OpenApi;

EnvFile.LoadNearest();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddCore(builder.Configuration, builder.Environment);
builder.Services.AddEnumCodeSchemas();
builder.Services.AddModules(
  builder.Configuration,
  new HealthModule(),
  new AuthModule(),
  new WorkspacesModule(),
  new ProjectsModule(),
  new BoardsModule(),
  new WorkModule());

var app = builder.Build();

app.UseCore();
app.MapModules();

app.Run();

public partial class Program;
