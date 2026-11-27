using Coordina.Api.Modules.Boards.Application;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Modules.Insights.Domain;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>Puts what the engine says about a board into words, dates and people.</summary>
internal static class BoardInsights
{
  public static BoardPlanResponse ToResponse(
    BoardPlan plan,
    GraphForecast<Guid> forecast,
    GraphCalibration calibration)
  {
    var milestonesByCard = forecast.Milestones
      .SelectMany(milestone => milestone.Cards.Select(card => (card.Node, Milestone: milestone.Node)))
      .ToLookup(entry => entry.Node, entry => entry.Milestone);
    var items = plan.Graph.WorkItems.ToDictionary(item => item.Node);

    return new BoardPlanResponse(
      plan.Board.Id,
      plan.Today,
      BoardMilestoneForecasts.ToResponses(plan, forecast),
      forecast.Plan
        .Select((card, index) => new BoardPlannedCardResponse(
          card.Node,
          plan.Cards[card.Node].Title,
          index + 1,
          card.Person is { } person ? plan.People[person] : null,
          plan.Start(card.StartDays),
          plan.Finish(card.FinishDays),
          items[card.Node].IsInProgress,
          items[card.Node].ElapsedHours,
          milestonesByCard[card.Node].ToArray()))
        .ToArray(),
      new BoardPlanAssumptionsResponse(
        BoardPlan.Simulation.RunCount,
        WorkingDays.FocusHoursPerDay,
        plan.Graph.History.Count,
        calibration.Growth));
  }

  public static BoardMilestoneActionResponse ToResponse(BoardPlan plan, GraphAction<Guid> action)
  {
    var kind = (MilestoneActionKind)(int)action.Kind;
    var card = kind is MilestoneActionKind.AddPerson ? null : plan.Cards[action.Node];

    return new BoardMilestoneActionResponse(
      EnumCode.Format(kind),
      card?.Id,
      card?.Title,
      kind is MilestoneActionKind.AssignCard ? plan.People[action.Person] : null,
      Math.Max(0, action.DroppedCount - 1),
      action.OnTimeBefore,
      action.OnTimeAfter,
      plan.Finish(action.P85DaysBefore),
      plan.Finish(action.P85DaysAfter),
      action.OthersDelayDays,
      action.ValueLost);
  }

  public static BoardMilestoneQuestionResponse ToResponse(BoardPlan plan, GraphQuestion<Guid> question)
  {
    var kind = (MilestoneQuestionKind)(int)question.Kind;
    var other = kind is MilestoneQuestionKind.Dependency ? plan.Cards[question.Other] : null;

    return new BoardMilestoneQuestionResponse(
      EnumCode.Format(kind),
      question.Node,
      plan.Cards[question.Node].Title,
      other?.Id,
      other?.Title,
      other is null ? null : question.Probability,
      other is null ? null : question.ShiftDays,
      question.MarginDays);
  }

  public static BoardCardEventResponse ToResponse(BoardCardEvent cardEvent) =>
    new(
      cardEvent.Id,
      cardEvent.CardId,
      EnumCode.Format(cardEvent.Kind),
      cardEvent.From,
      cardEvent.To,
      cardEvent.OccurredAt);
}
