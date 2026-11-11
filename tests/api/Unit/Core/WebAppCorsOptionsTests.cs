using Coordina.Api.Core.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Coordina.Api.Tests.Unit.Core;

public sealed class WebAppCorsOptionsTests
{
  [Theory]
  [InlineData("Production")]
  [InlineData("Staging")]
  public void NoOrigins_OutsideDevelopment_FailsAtStartup(string environment)
  {
    var exception = Assert.Throws<InvalidOperationException>(
      () => new WebAppCorsOptions().Validate(Environment(environment)));

    Assert.Contains("Cors__AllowedOrigins__0", exception.Message, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("Development")]
  [InlineData(CoreHostingExtensions.TestingEnvironment)]
  public void NoOrigins_OnADeveloperMachine_IsAllowed(string environment)
  {
    new WebAppCorsOptions().Validate(Environment(environment));
  }

  [Theory]
  [InlineData("https://app.example.com")]
  [InlineData("http://localhost:5173")]
  [InlineData("http://127.0.0.1:5174")]
  public void Origin_IsAccepted(string origin)
  {
    new WebAppCorsOptions { AllowedOrigins = [origin] }.Validate(Environment(Environments.Production));
  }

  [Theory]
  [InlineData("https://app.example.com/")]
  [InlineData("https://app.example.com/board")]
  [InlineData("app.example.com")]
  [InlineData("*")]
  [InlineData("ftp://app.example.com")]
  public void ValueThatIsNotAnOrigin_FailsAtStartup(string origin)
  {
    var options = new WebAppCorsOptions { AllowedOrigins = ["https://app.example.com", origin] };

    var exception = Assert.Throws<InvalidOperationException>(
      () => options.Validate(Environment(Environments.Production)));

    Assert.Contains($"'{origin}'", exception.Message, StringComparison.Ordinal);
  }

  private static HostingEnvironment Environment(string name) => new() { EnvironmentName = name };
}
