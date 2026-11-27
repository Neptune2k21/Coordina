using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Application.Planning;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Unit.Tests.Boards;

/// <summary>
/// The templates hold real cards and dependencies someone wrote by hand: hidden
/// from the engine, they tell whether its guesses are worth showing.
/// </summary>
public sealed class DependencySuggestionTests
{
  private const int Shown = 3;

  private readonly NativeGuidGraphEngine _graphEngine = new();

  [Fact]
  public void SuggestDependencies_FindsHiddenDependenciesMoreOftenThanChance()
  {
    var found = 0;
    var expectedByChance = 0.0;

    foreach (var cards in BoardTemplateCatalog.All.Select(template => OpenCards(template.Lists)))
    {
      var ids = cards.ToDictionary(card => card.Seed.Key, _ => Guid.NewGuid());
      var board = Board(cards, ids);
      var tokens = CardTokens.Of(cards.Select(card => PlanningBoards.Card(card.Seed.Title) with
      {
        Id = ids[card.Seed.Key],
        Description = card.Seed.Description
      }));

      foreach (var card in cards)
      {
        var shown = _graphEngine
          .SuggestDependencies(board, tokens, ids[card.Seed.Key], Shown)
          .Select(hint => hint.Dependency)
          .ToArray();

        foreach (var dependency in card.Seed.DependsOn.Where(ids.ContainsKey))
        {
          found += shown.Contains(ids[dependency]) ? 1 : 0;
          expectedByChance += Math.Min(1.0, (double)Shown / (cards.Length - 1));
        }
      }
    }

    Assert.True(
      found >= expectedByChance * 1.3,
      $"Found {found} of the hidden dependencies; showing cards at random would find {expectedByChance:F1}.");
  }

  private static TemplateCard[] OpenCards(IEnumerable<BoardListTemplate> lists) =>
    lists
      .Where(list => list.TaskStatus is not BoardTaskStatus.Done)
      .SelectMany(list => list.Cards.Select(card => new TemplateCard(
        card,
        list.TaskStatus is BoardTaskStatus.InProgress)))
      .ToArray();

  private static GraphBoard<Guid> Board(TemplateCard[] cards, Dictionary<string, Guid> ids)
  {
    var labels = cards
      .SelectMany(card => card.Seed.Labels)
      .Distinct(StringComparer.Ordinal)
      .Select((label, index) => (label, index))
      .ToDictionary(entry => entry.label, entry => entry.index);

    return new GraphBoard<Guid>(
      ids.Values,
      [],
      cards
        .Select((card, index) => new GraphWorkItem<Guid>(
          ids[card.Seed.Key],
          false,
          0,
          GraphWorkItem<Guid>.NoDueDate,
          0,
          0,
          index,
          IsInProgress: card.IsInProgress))
        .ToArray(),
      [],
      cards
        .SelectMany(card => card.Seed.Labels.Select(label =>
          new GraphCardLabel<Guid>(ids[card.Seed.Key], labels[label])))
        .ToArray(),
      [],
      [],
      GraphScopeGrowth.None,
      1);
  }

  private sealed record TemplateCard(BoardCardTemplate Seed, bool IsInProgress);
}
