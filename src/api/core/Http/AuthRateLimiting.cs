using System.Globalization;
using System.Threading.RateLimiting;
using Coordina.Api.Core.Hosting;
using Microsoft.AspNetCore.RateLimiting;

namespace Coordina.Api.Core.Http;

/// <summary>
/// Limits the anonymous endpoints that check a password or create an account,
/// per client IP, against password guessing and sign-up spam. Endpoints opt in
/// with <c>RequireRateLimiting(AuthRateLimiting.Policy)</c>.
/// </summary>
public static class AuthRateLimiting
{
  public const string Policy = "auth";

  public static IServiceCollection AddAuthRateLimiting(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(environment);

    var section = configuration.GetSection(AuthRateLimitOptions.SectionName);
    var options = section.Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();
    options.Validate();

    // Every test client shares one address, so a limit would make tests depend
    // on each other. Tests that check the limit configure one explicitly.
    var isLimited = !environment.IsEnvironment(CoreHostingExtensions.TestingEnvironment) || section.Exists();

    return services.AddRateLimiter(limiter =>
    {
      limiter.OnRejected = RejectAsync;
      limiter.AddPolicy(Policy, context => isLimited
        ? RateLimitPartition.GetFixedWindowLimiter(
          context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          _ => new FixedWindowRateLimiterOptions
          {
            PermitLimit = options.PermitLimit,
            Window = TimeSpan.FromSeconds(options.WindowSeconds),
            QueueLimit = 0
          })
        : RateLimitPartition.GetNoLimiter(string.Empty));
    });
  }

  private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
  {
    var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
      ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
      : (int?)null;

    if (seconds is not null)
    {
      context.HttpContext.Response.Headers.RetryAfter = seconds.Value.ToString(CultureInfo.InvariantCulture);
    }

    await TypedResults.Problem(
      seconds is null
        ? "Too many attempts. Wait a moment, then try again."
        : $"Too many attempts. Try again in {seconds} second{(seconds == 1 ? string.Empty : "s")}.",
      statusCode: StatusCodes.Status429TooManyRequests)
      .ExecuteAsync(context.HttpContext);
  }
}
