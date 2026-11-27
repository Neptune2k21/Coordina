namespace Coordina.Api.Modules.Boards.Application.Graph;

// The calls that advise: what to do about a milestone, what to ask, what the
// board probably misses, and what its history has taught.
public sealed partial class NativeGuidGraphEngine
{
  private const int SizeCount = 6;
  private const int LearnedLabelLimit = 32;

  public IReadOnlyList<GraphAction<Guid>> ProposeActions(
    GraphBoard<Guid> board,
    IReadOnlyCollection<int> receivers,
    GraphActionOptions<Guid> options)
  {
    ArgumentNullException.ThrowIfNull(options);

    var nativeReceivers = ToNativePeople(receivers);
    var native = new NativeGraphActionOptions(
      ToNative(options.Milestone),
      ToNative(options.Simulation),
      checked((uint)options.ScreenRunCount),
      checked((uint)options.Limit));
    var actions = new NativeGraphAction[options.Limit];
    var output = new NativeGraphActionOutput { ActionCapacity = ToNativeCount(actions.Length) };

    Run(
      "propose_actions",
      board,
      [actions],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Actions = buffers[0];
        return NativeMethods.ProposeActions(
          ref input,
          nativeReceivers,
          ToNativeCount(nativeReceivers.Length),
          ref native,
          ref output);
      });

    return Written(
      actions,
      output.ActionCount,
      action => new GraphAction<Guid>(
        (GraphActionKind)action.Kind,
        FromNative(action.Node),
        checked((int)action.Person),
        action.OnTimeBefore,
        action.OnTimeAfter,
        action.P85DaysBefore,
        action.P85DaysAfter,
        action.OthersDelayDays,
        checked((int)action.DroppedCount),
        action.ValueLost));
  }

  public GraphQuestions<Guid> RankQuestions(
    GraphBoard<Guid> board,
    IReadOnlyCollection<GraphCardToken<Guid>> tokens,
    IReadOnlyCollection<GraphDependencyHint<Guid>> hints,
    GraphQuestionOptions<Guid> options)
  {
    ArgumentNullException.ThrowIfNull(hints);
    ArgumentNullException.ThrowIfNull(options);

    var nativeTokens = ToNativeTokens(tokens);
    var nativeHints = hints
      .Select(hint => new NativeGraphDependencyHint(
        ToNative(hint.Card),
        ToNative(hint.Dependency),
        hint.Probability))
      .ToArray();
    var native = new NativeGraphQuestionOptions(
      ToNative(options.Milestone),
      ToNative(options.Simulation),
      checked((uint)options.Limit),
      0U);
    var questions = new NativeGraphQuestion[options.Limit];
    var output = new NativeGraphQuestionOutput { QuestionCapacity = ToNativeCount(questions.Length) };

    Run(
      "rank_questions",
      board,
      [questions],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Questions = buffers[0];
        return NativeMethods.RankQuestions(
          ref input,
          nativeTokens,
          ToNativeCount(nativeTokens.Length),
          nativeHints,
          ToNativeCount(nativeHints.Length),
          ref native,
          ref output);
      });

    return new GraphQuestions<Guid>(
      output.MarginDays,
      Written(
        questions,
        output.QuestionCount,
        question => new GraphQuestion<Guid>(
          (GraphQuestionKind)question.Kind,
          FromNative(question.Node),
          FromNative(question.Other),
          question.Probability,
          question.MarginDays,
          question.ShiftDays)));
  }

  public IReadOnlyList<GraphDependencyHint<Guid>> SuggestDependencies(
    GraphBoard<Guid> board,
    IReadOnlyCollection<GraphCardToken<Guid>> tokens,
    Guid source,
    int limit)
  {
    var nativeTokens = ToNativeTokens(tokens);
    var nativeSource = ToNative(source);
    var hints = new NativeGraphDependencyHint[limit];
    var output = new NativeGraphHintOutput { HintCapacity = ToNativeCount(hints.Length) };

    Run(
      "suggest_dependencies",
      board,
      [hints],
      (ref NativeGraphBoard input, IntPtr[] buffers) =>
      {
        output.Hints = buffers[0];
        return NativeMethods.SuggestDependencies(
          ref input,
          nativeTokens,
          ToNativeCount(nativeTokens.Length),
          ref nativeSource,
          checked((uint)limit),
          ref output);
      });

    return Written(
      hints,
      output.HintCount,
      hint => new GraphDependencyHint<Guid>(
        FromNative(hint.Card),
        FromNative(hint.Dependency),
        hint.Probability));
  }

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

  private static NativeGraphCardToken[] ToNativeTokens(IReadOnlyCollection<GraphCardToken<Guid>> tokens)
  {
    ArgumentNullException.ThrowIfNull(tokens);

    return tokens
      .Select(token => new NativeGraphCardToken(ToNative(token.Node), token.Weight, token.Token, 0U))
      .ToArray();
  }
}
