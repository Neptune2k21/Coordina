using Coordina.Api.Modules.Health;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
  message = "Bienvenue sur l'API de Coordina !"
}));

app.MapHealthEndpoints();

app.Run();

public partial class Program;
