namespace Coordina.Api.Integration.Tests.Support;

/// <summary>Tests that measure time run alone, so the other classes do not slow them down.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingTestGroup
{
  public const string Name = "timing";
}
