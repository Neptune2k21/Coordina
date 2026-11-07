using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Tests.Unit.Boards;

public sealed class BoardTemplateCatalogTests
{
  private const int WipLimit = 5;

  public static TheoryData<BoardTemplate> Templates =>
    new(BoardTemplateCatalog.All.Select(template => template.Id));

  [Fact]
  public void Catalog_CoversEveryTemplateExceptCustomAndBlank()
  {
    var expected = Enum.GetValues<BoardTemplate>()
      .Where(template => template is not (BoardTemplate.Custom or BoardTemplate.Blank));

    Assert.Equal(expected, BoardTemplateCatalog.All.Select(template => template.Id));
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_HasAListForEveryStatus(BoardTemplate template)
  {
    var statuses = BoardTemplateCatalog.GetLists(template)
      .Select(list => list.TaskStatus)
      .ToHashSet();

    Assert.Equal(Enum.GetValues<BoardTaskStatus>().ToHashSet(), statuses);
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_CardKeysAreUniqueAndDependenciesResolve(BoardTemplate template)
  {
    var cards = CardsOf(template);
    var keys = cards.Select(card => card.Key).ToArray();

    Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    Assert.All(cards, card =>
    {
      Assert.DoesNotContain(card.Key, card.DependsOn);
      Assert.Equal(
        card.DependsOn.Count,
        card.DependsOn.Distinct(StringComparer.Ordinal).Count());
      Assert.All(card.DependsOn, key => Assert.Contains(key, keys));
    });
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_DependencyGraphIsAcyclic(BoardTemplate template)
  {
    var remaining = CardsOf(template).ToDictionary(card => card.Key, card => card.DependsOn);

    while (remaining.Count > 0)
    {
      var free = remaining
        .Where(card => !card.Value.Any(remaining.ContainsKey))
        .Select(card => card.Key)
        .ToArray();

      Assert.True(
        free.Length > 0,
        $"Cycle between: {string.Join(", ", remaining.Keys)}");

      foreach (var key in free)
      {
        remaining.Remove(key);
      }
    }
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_CardsAreNotAheadOfWhatTheyWaitFor(BoardTemplate template)
  {
    var statusByKey = BoardTemplateCatalog.GetLists(template)
      .SelectMany(list => list.Cards.Select(card => (card.Key, list.TaskStatus)))
      .ToDictionary(item => item.Key, item => item.TaskStatus);

    Assert.All(CardsOf(template), card => Assert.All(
      card.DependsOn,
      key => Assert.True(
        Progress(statusByKey[key]) >= Progress(statusByKey[card.Key]),
        $"'{card.Key}' is {statusByKey[card.Key]} but waits for '{key}', which is {statusByKey[key]}.")));
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_ContentIsRealistic(BoardTemplate template)
  {
    var cards = CardsOf(template);

    Assert.Contains(cards, card => card.DependsOn.Count > 0);
    Assert.Contains(cards, card => card.Subtasks.Count > 0);
    Assert.Contains(cards, card => card.DueInDays is not null);
    Assert.All(cards, card => Assert.NotEmpty(card.Labels));
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_ContentRespectsBoardRules(BoardTemplate template)
  {
    Assert.All(CardsOf(template), card =>
    {
      Assert.InRange(card.Title.Length, 1, 180);
      Assert.InRange(card.Description.Length, 1, 2000);
      Assert.InRange(card.EstimatedMinutes, 5, 2880);
      Assert.InRange(card.Labels.Count, 0, 12);
      Assert.All(card.Labels, label => Assert.InRange(label.Length, 1, 32));
      Assert.All(card.Subtasks, title => Assert.InRange(title.Length, 1, 180));
      Assert.True(card.DueInDays is null or > 0, $"'{card.Key}' is already overdue.");
    });

    Assert.All(
      BoardTemplateCatalog.GetLists(template)
        .Where(list => list.TaskStatus == BoardTaskStatus.InProgress),
      list => Assert.InRange(list.Cards.Count, 0, WipLimit));
  }

  [Theory]
  [MemberData(nameof(Templates))]
  public void Template_DoneCardsHaveNoDueDate(BoardTemplate template)
  {
    var doneCards = BoardTemplateCatalog.GetLists(template)
      .Where(list => list.TaskStatus == BoardTaskStatus.Done)
      .SelectMany(list => list.Cards);

    Assert.All(doneCards, card => Assert.Null(card.DueInDays));
  }

  private static BoardCardTemplate[] CardsOf(BoardTemplate template) =>
    BoardTemplateCatalog.GetLists(template)
      .SelectMany(list => list.Cards)
      .ToArray();

  private static int Progress(BoardTaskStatus status) => status switch
  {
    BoardTaskStatus.Done => 2,
    BoardTaskStatus.InProgress => 1,
    _ => 0
  };
}
