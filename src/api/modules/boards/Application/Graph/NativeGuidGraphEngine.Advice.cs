namespace Coordina.Api.Modules.Boards.Application.Graph;

// The calls that advise: for now, what a board's history has taught.
public sealed partial class NativeGuidGraphEngine
{
  private const int SizeCount = 6;
  private const int LearnedLabelLimit = 32;

  public GraphCalibration Calibrate(GraphBoard<Guid> board)
  {
    var sizes = new NativeGraphDurationRatio[SizeCount];
    var labels = new NativeGraphDurationRatio[LearnedLabelLimit];
    var output = new NativeGraphCalibration
    {
      SizeCapacity = ToNativeCount(sizes.Length),
      LabelCapacity = ToNativeCount(labels.Length)
    };

    Run(
      "calibrate",
      board,
      [sizes, labels],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Sizes = buffers[0];
        output.Labels = buffers[1];
        return NativeMethods.Calibrate(ref input, ref output);
      });

    return new GraphCalibration(
      FromNative(output.Team),
      Written(sizes, output.SizeCount, FromNative),
      Written(labels, output.LabelCount, FromNative),
      output.WithinP50,
      output.WithinP85,
      checked((int)output.ReplayedCount),
      output.Growth);
  }

  private static GraphDurationRatio FromNative(NativeGraphDurationRatio ratio) =>
    new(
      checked((int)ratio.Key),
      ratio.Low,
      ratio.Median,
      ratio.High,
      checked((int)ratio.CardCount));
}
