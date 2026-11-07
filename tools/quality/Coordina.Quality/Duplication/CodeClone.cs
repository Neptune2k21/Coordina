namespace Coordina.Quality.Duplication;

/// <summary>Two places in the repository with the same code, as repository-relative paths.</summary>
internal sealed record CodeClone(
  string FirstPath,
  int FirstLine,
  string SecondPath,
  int SecondLine,
  int Lines);
