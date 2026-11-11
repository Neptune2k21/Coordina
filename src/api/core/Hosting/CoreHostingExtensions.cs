using System.Text.Json.Serialization;
using Coordina.Api.Core.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;

namespace Coordina.Api.Core.Hosting;

/// <summary>Cross-cutting services and middleware shared by every module.</summary>
public static class CoreHostingExtensions
{
  public const string TestingEnvironment = "Testing";
  private const string WebAppCorsPolicy = "WebApp";

  public static IServiceCollection AddCore(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(environment);

    var cors = configuration
      .GetSection(WebAppCorsOptions.SectionName)
      .Get<WebAppCorsOptions>() ?? new WebAppCorsOptions();
    cors.Validate(environment);

    services.TryAddSingleton(TimeProvider.System);
    services.AddProblemDetails();

    // Numbers are JSON numbers. The web defaults also accept "5" for 5, which
    // the contract then has to describe as "number or string" everywhere.
    services.ConfigureHttpJsonOptions(options =>
      options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
    services.AddCors(options =>
    {
      options.AddPolicy(WebAppCorsPolicy, policy =>
      {
        // Credentials let the web app send and receive the refresh cookie.
        policy
          .WithOrigins(cors.AllowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
      });
    });

    if (!environment.IsEnvironment(TestingEnvironment))
    {
      services.AddPostgres(configuration);
    }

    services.AddAuthorization();
    services.AddOpenApi();

    return services;
  }

  public static WebApplication UseCore(this WebApplication app)
  {
    ArgumentNullException.ThrowIfNull(app);

    // Development keeps the detailed exception page; elsewhere unhandled
    // exceptions become a ProblemDetails 500 with a trace id and no internals.
    if (!app.Environment.IsDevelopment())
    {
      app.UseExceptionHandler();
    }

    app.UseCors(WebAppCorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference("/api-docs", options =>
    {
      options.Title = "Coordina API";
    })
    .AllowAnonymous();

    return app;
  }
}
