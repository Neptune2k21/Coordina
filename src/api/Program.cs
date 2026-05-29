using Coordina.Api.Infrastructure.Configuration;
using Coordina.Api.Infrastructure.Persistence;
using Coordina.Api.Modules.Auth;
using Coordina.Api.Modules.Health;

EnvFile.LoadNearest();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddCors(options =>
{
  options.AddPolicy("WebApp", policy =>
  {
    policy
      .WithOrigins(
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174")
      .AllowAnyHeader()
      .AllowAnyMethod();
  });
});
builder.Services.AddPostgres(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("WebApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
  message = "Bienvenue sur l'API de Coordina !"
}));

app.MapAuthEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
