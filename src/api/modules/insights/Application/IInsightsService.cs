using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Insights.Contracts;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>
/// What the engine makes of a board, for people and assistants: its plan, what
/// to do, what to ask, what a change would do, and what it has learned. None
/// of it changes the board.
/// </summary>
public interface IInsightsService
{
  Task<Result<BoardPlanResponse>> GetPlanAsync(BoardTarget target, CancellationToken cancellationToken);

  Task<Result<IReadOnlyCollection<BoardMilestoneActionResponse>>> ProposeActionsAsync(
    BoardTarget target,
    Guid milestoneId,
    CancellationToken cancellationToken);

  Task<Result<BoardMilestoneQuestionsResponse>> RankQuestionsAsync(
    BoardTarget target,
    Guid milestoneId,
    BoardMilestoneQuestionsRequest request,
    CancellationToken cancellationToken);

  Task<Result<BoardWhatIfResponse>> TryChangesAsync(
    BoardTarget target,
    BoardWhatIfRequest request,
    CancellationToken cancellationToken);

  Task<Result<BoardLearningResponse>> GetLearningAsync(
    BoardTarget target,
    CancellationToken cancellationToken);

  Task<Result<BoardEventPageResponse>> ListEventsAsync(
    BoardTarget target,
    long after,
    int limit,
    CancellationToken cancellationToken);
}
