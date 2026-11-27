namespace Coordina.Api.Modules.Boards.Application.Graph;

/// <summary>
/// What the board's history has taught, and the share of replayed cards that
/// took no longer than their 50% and 85% durations.
/// </summary>
public sealed record GraphCalibration(
  GraphDurationRatio Team,
  IReadOnlyList<GraphDurationRatio> Sizes,
  IReadOnlyList<GraphDurationRatio> Labels,
  double WithinP50,
  double WithinP85,
  int ReplayedCount,
  double Growth);

/// <summary>
/// How long cards take against what their size says (1.6 is 60% longer), with
/// a tenth of the chance below low and above high. The key is the size's or
/// the label's number.
/// </summary>
public sealed record GraphDurationRatio(int Key, double Low, double Median, double High, int CardCount);
