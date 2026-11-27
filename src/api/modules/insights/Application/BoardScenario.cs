using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Boards.Domain;
using Coordina.Api.Modules.Insights.Contracts;
using Coordina.Api.Modules.Insights.Domain;
using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Modules.Insights.Application;

/// <summary>A board as it would be with some changes, which are never saved.</summary>
internal sealed record BoardScenario(ProjectBoard Board, int ExtraPeople)
{
  private const int ChangeLimit = 100;
  private const int ExtraPeopleLimit = 20;

  /// <summary>Applies the changes in order; the error names the first one that cannot be.</summary>
  public static Result<BoardScenario> Apply(
    ProjectBoard board,
    IReadOnlyCollection<BoardWhatIfChangeRequest> changes,
    IReadOnlySet<Guid> members,
    DateOnly today)
  {
    if (changes.Count is 0 or > ChangeLimit)
    {
      return Error.Validation(
        nameof(BoardWhatIfRequest.Changes),
        $"Give between 1 and {ChangeLimit} changes.");
    }

    var scenario = new BoardScenario(board, 0);

    foreach (var (change, index) in changes.Select((change, index) => (change, index)))
    {
      var next = scenario.Apply(change, members, today);

      if (next is not { } applied)
      {
        return Error.Validation(
          $"{nameof(BoardWhatIfRequest.Changes)}[{index}]",
          $"This change needs a kind ({EnumCode.Describe<WhatIfChangeKind>()}) and valid values for it.");
      }

      scenario = applied;
    }

    return Result.Success(scenario);
  }

  private BoardScenario? Apply(
    BoardWhatIfChangeRequest change,
    IReadOnlySet<Guid> members,
    DateOnly today)
  {
    if (!EnumCode.TryParse<WhatIfChangeKind>(change.Kind, out var kind))
    {
      return null;
    }

    return kind switch
    {
      WhatIfChangeKind.SetSize => SetSize(change),
      WhatIfChangeKind.Assign => Assign(change, members),
      WhatIfChangeKind.AddDependency => Link(change, AddDependency),
      WhatIfChangeKind.RemoveDependency => Link(change, RemoveDependency),
      WhatIfChangeKind.SetTarget when change.Date >= today =>
        Change(change.CardId, card => card with { MilestoneDate = change.Date }),
      WhatIfChangeKind.Complete => Change(
        change.CardId,
        card => card with { IsCompleted = true }),
      WhatIfChangeKind.AddPeople when change.Count is > 0 and <= ExtraPeopleLimit =>
        this with { ExtraPeople = Math.Min(ExtraPeopleLimit, ExtraPeople + change.Count.Value) },
      _ => null
    };
  }

  private BoardScenario? SetSize(BoardWhatIfChangeRequest change)
  {
    if (change.Size is null)
    {
      return Change(change.CardId, card => card with { Size = null });
    }

    return EnumCode.TryParse<CardSize>(change.Size, out var size)
      ? Change(change.CardId, card => card with { Size = size })
      : null;
  }

  private BoardScenario? Assign(BoardWhatIfChangeRequest change, IReadOnlySet<Guid> members)
  {
    if (change.UserIds is not { } userIds || !userIds.All(members.Contains))
    {
      return null;
    }

    return Change(
      change.CardId,
      card => card with
      {
        Assignees = userIds
          .Distinct()
          .Select(userId => new ProjectBoardCardAssignee(userId, null, null))
          .ToArray()
      });
  }

  private BoardScenario? Link(
    BoardWhatIfChangeRequest change,
    Func<ProjectBoardCard, ProjectBoardCard, ProjectBoardCard> update)
  {
    var dependency = Find(change.DependsOnCardId);

    return dependency is null || change.CardId == change.DependsOnCardId
      ? null
      : Change(change.CardId, card => update(card, dependency));
  }

  private static ProjectBoardCard AddDependency(ProjectBoardCard card, ProjectBoardCard dependency) =>
    card.Dependencies.Any(existing => existing.CardId == dependency.Id)
      ? card
      : card with
      {
        Dependencies =
        [
          .. card.Dependencies,
          new ProjectBoardCardDependency(dependency.Id, dependency.Title, dependency.IsCompleted)
        ]
      };

  private static ProjectBoardCard RemoveDependency(ProjectBoardCard card, ProjectBoardCard dependency) =>
    card with
    {
      Dependencies = card.Dependencies.Where(existing => existing.CardId != dependency.Id).ToArray()
    };

  private ProjectBoardCard? Find(Guid? cardId) =>
    Board.Lists.SelectMany(list => list.Cards).FirstOrDefault(card => card.Id == cardId);

  private BoardScenario? Change(Guid? cardId, Func<ProjectBoardCard, ProjectBoardCard> update) =>
    Find(cardId) is null
      ? null
      : this with
      {
        Board = Board with
        {
          Lists = Board.Lists
            .Select(list => list with
            {
              Cards = list.Cards.Select(card => card.Id == cardId ? update(card) : card).ToArray()
            })
            .ToArray()
        }
      };
}
