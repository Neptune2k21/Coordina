using System.Text.Json.Serialization;

namespace Coordina.Quality.Baselines;

internal sealed record BaselineEntry(string Rule, string Path, string Message, int Count)
{
  [JsonIgnore]
  public string Key => $"{Rule}|{Path}|{Message}";
}
