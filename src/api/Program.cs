using Coordina.Api.modules.health;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "Bienvenue sur l'API de Coordina !"
}));

app.MapHealthEndpoints();

app.Run();
