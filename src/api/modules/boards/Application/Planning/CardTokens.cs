using System.Globalization;
using System.Text;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Modules.Boards.Domain;

namespace Coordina.Api.Modules.Boards.Application.Planning;

/// <summary>
/// The words of cards, as numbers the engine compares. Case, accents and
/// plurals are dropped, so "Détails des factures" and "facture detaillée" share
/// a word; the engine itself weighs how rare each word is.
/// </summary>
internal static class CardTokens
{
  private const double TitleWeight = 2;
  private const double DescriptionWeight = 1;
  private const int ShortestWord = 3;

  // Words this long keep their first letters only: what follows is mostly
  // the ending that plurals, genders and tenses change.
  private const int StemLength = 6;

  public static GraphCardToken<Guid>[] Of(IEnumerable<ProjectBoardCard> cards) =>
    cards
      .SelectMany(card => Of(card.Id, card.Title, TitleWeight)
        .Concat(Of(card.Id, card.Description, DescriptionWeight)))
      .ToArray();

  private static IEnumerable<GraphCardToken<Guid>> Of(Guid cardId, string? text, double weight) =>
    Words(text)
      .GroupBy(word => word, StringComparer.Ordinal)
      .Select(group => new GraphCardToken<Guid>(cardId, Hash(group.Key), weight * group.Count()));

  private static IEnumerable<string> Words(string? text)
  {
    var word = new StringBuilder();

    foreach (var character in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
    {
      if (char.IsLetterOrDigit(character))
      {
        word.Append(char.ToLowerInvariant(character));
      }
      else if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
      {
        if (word.Length >= ShortestWord)
        {
          yield return Stem(word);
        }

        word.Clear();
      }
    }

    if (word.Length >= ShortestWord)
    {
      yield return Stem(word);
    }
  }

  private static string Stem(StringBuilder word)
  {
    var length = Math.Min(word.Length, StemLength);

    return word.ToString(0, length).TrimEnd('s');
  }

  // FNV-1a: the same word has the same number in every process, unlike
  // string.GetHashCode.
  private static uint Hash(string word)
  {
    var hash = 2166136261U;

    foreach (var character in word)
    {
      hash = unchecked((hash ^ character) * 16777619U);
    }

    return hash;
  }
}
