using Coordina.Api.Core.Configuration;

namespace Coordina.Api.Unit.Tests.Core;

public sealed class EnvFileTests : IDisposable
{
  private readonly string _fileName = $"{Guid.NewGuid():N}.env";
  private readonly string _key = $"COORDINA_TEST_{Guid.NewGuid():N}";

  [Fact]
  public void Load_SetsAVariableTheProcessDoesNotHave()
  {
    Write($"# comment\n\n{_key} = \"from file\"\nnot a variable\n");

    EnvFile.LoadNearest(_fileName);

    Assert.Equal("from file", Environment.GetEnvironmentVariable(_key));
  }

  [Fact]
  public void Load_KeepsAVariableTheProcessAlreadyHas()
  {
    Environment.SetEnvironmentVariable(_key, "from process");
    Write($"{_key}=from file\n");

    EnvFile.LoadNearest(_fileName);

    Assert.Equal("from process", Environment.GetEnvironmentVariable(_key));
  }

  [Fact]
  public void Load_WithoutAFile_ChangesNothing()
  {
    EnvFile.LoadNearest(_fileName);

    Assert.Null(Environment.GetEnvironmentVariable(_key));
  }

  public void Dispose()
  {
    File.Delete(_fileName);
    Environment.SetEnvironmentVariable(_key, null);
  }

  private void Write(string content) => File.WriteAllText(_fileName, content);
}
