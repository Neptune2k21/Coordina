using Coordina.Api.Modules.Auth.Infrastructure;

namespace Coordina.Api.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
  [Fact]
  public void Verify_WithMatchingPassword_ReturnsTrue()
  {
    var hasher = new Pbkdf2PasswordHasher();
    var hash = hasher.Hash("Password123!");

    Assert.True(hasher.Verify("Password123!", hash));
  }

  [Fact]
  public void Verify_WithDifferentPassword_ReturnsFalse()
  {
    var hasher = new Pbkdf2PasswordHasher();
    var hash = hasher.Hash("Password123!");

    Assert.False(hasher.Verify("WrongPassword123!", hash));
  }
}
