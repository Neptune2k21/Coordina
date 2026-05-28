var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => new
{
    App = "Coordina",
    Message = "API is running"
});

app.Run();
