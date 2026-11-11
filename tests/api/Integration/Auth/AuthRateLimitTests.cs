using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Coordina.Api.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Coordina.Api.Tests.Integration.Auth;

public sealed class AuthRateLimitTests(ApiTestApplicationFactory factory)
  : ApiTest(factory)
{
  private const int PermitLimit = 3;

  [Fact]
  public async Task Login_BeyondTheLimit_Returns429WithRetryAfter()
  {
    using var client = LimitedClient();

    for (var attempt = 0; attempt < PermitLimit; attempt++)
    {
      using var allowed = await LoginAsync(client);
      Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
    }

    using var rejected = await LoginAsync(client);

    Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    var retryAfter = int.Parse(Assert.Single(rejected.Headers.GetValues("Retry-After")), CultureInfo.InvariantCulture);
    Assert.InRange(retryAfter, 1, 60);

    var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
    Assert.NotNull(problem);
    Assert.Equal(StatusCodes.Status429TooManyRequests, problem.Status);
    Assert.StartsWith("Too many attempts. Try again in ", problem.Detail, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Register_SharesTheLimitWithLogin()
  {
    using var client = LimitedClient();

    for (var attempt = 0; attempt < PermitLimit; attempt++)
    {
      using var allowed = await LoginAsync(client);
    }

    using var register = await client.PostAsJsonAsync(
      "/auth/register",
      new { name = "Spammer", email = ApiTestClient.UniqueEmail(), password = ApiTestClient.Password });

    Assert.Equal(HttpStatusCode.TooManyRequests, register.StatusCode);
  }

  [Fact]
  public async Task OtherEndpoints_AreNotLimited()
  {
    using var client = LimitedClient();

    for (var attempt = 0; attempt <= PermitLimit; attempt++)
    {
      using var login = await LoginAsync(client);
    }

    using var health = await client.GetAsync("/health/live");
    using var refresh = await client.PostAsync("/auth/refresh", null);

    Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    Assert.NotEqual(HttpStatusCode.TooManyRequests, refresh.StatusCode);
  }

  [Fact]
  public async Task TestingEnvironment_WithoutAConfiguredLimit_IsNotLimited()
  {
    for (var attempt = 0; attempt < 20; attempt++)
    {
      using var login = await LoginAsync(Api.Http);
      Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
  }

  // A host of its own, so its limit does not carry over between tests.
  private HttpClient LimitedClient() =>
    Factory
      .WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Auth:PermitLimit", PermitLimit.ToString(CultureInfo.InvariantCulture)))
      .CreateClient();

  private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
    client.PostAsJsonAsync("/auth/login", new { email = "nobody@coordina.test", password = "WrongPassword1!" });
}
