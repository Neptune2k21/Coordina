using Coordina.Api.Core.Http;

namespace Coordina.Api.Tests.Unit.Core;

public sealed class AuthRateLimitOptionsTests
{
  [Fact]
  public void Defaults_AreValid()
  {
    var options = new AuthRateLimitOptions();

    options.Validate();
    Assert.Equal(10, options.PermitLimit);
    Assert.Equal(60, options.WindowSeconds);
  }

  [Theory]
  [InlineData(0, 60)]
  [InlineData(10, 0)]
  [InlineData(-1, -1)]
  public void LimitBelowOne_FailsAtStartup(int permitLimit, int windowSeconds)
  {
    var options = new AuthRateLimitOptions { PermitLimit = permitLimit, WindowSeconds = windowSeconds };

    Assert.Throws<InvalidOperationException>(options.Validate);
  }
}
