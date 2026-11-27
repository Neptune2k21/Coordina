using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Insights.Contracts;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>
/// What the engine makes of a board, for people and assistants: what it has
/// learned, and the history it learned from. None of it changes the board.
/// </summary>
public interface IInsightsService
{
  Task<Result<BoardLearningResponse>> GetLearningAsync(
    BoardTarget target,
    CancellationToken cancellationToken);

  Task<Result<BoardEventPageResponse>> ListEventsAsync(
    BoardTarget target,
    long after,
    int limit,
    CancellationToken cancellationToken);
}
