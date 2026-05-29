using System.Text;
using Coordina.Api.Modules.Auth.Application;
using Coordina.Api.Modules.Auth.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Coordina.Api.Modules.Auth;

public static class AuthModule
{
  public static IServiceCollection AddAuthModule(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var authOptions = configuration
      .GetSection(AuthOptions.SectionName)
      .Get<AuthOptions>() ?? throw new InvalidOperationException(
        "Auth configuration is missing.");
    authOptions.Validate();

    services.AddSingleton(authOptions);
    services.AddSingleton<IUserStore, PostgresUserStore>();
    services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
    services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddHostedService<AuthSchemaInitializer>();

    var signingKey = new SymmetricSecurityKey(
      Encoding.UTF8.GetBytes(authOptions.SigningKey));

    services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = authOptions.Issuer,
          ValidateAudience = true,
          ValidAudience = authOptions.Audience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = signingKey,
          ValidateLifetime = true,
          ClockSkew = TimeSpan.FromSeconds(30)
        };
      });

    return services;
  }

  public static IServiceCollection AddInMemoryAuthStoreForTests(
    this IServiceCollection services)
  {
    var userStoreDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(IUserStore));

    if (userStoreDescriptor is not null)
    {
      services.Remove(userStoreDescriptor);
    }

    var dataSourceDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource));

    if (dataSourceDescriptor is not null)
    {
      services.Remove(dataSourceDescriptor);
    }

    var initializerDescriptor = services.SingleOrDefault(
      descriptor => descriptor.ImplementationType == typeof(AuthSchemaInitializer));

    if (initializerDescriptor is not null)
    {
      services.Remove(initializerDescriptor);
    }

    services.AddSingleton<IUserStore, InMemoryUserStore>();

    return services;
  }
}
