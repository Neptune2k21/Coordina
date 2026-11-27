using Coordina.Api.Modules.Boards.Contracts;
using Coordina.Api.Modules.Insights.Domain;
using Coordina.Api.Shared.OpenApi;

namespace Coordina.Api.Modules.Insights.Contracts;

/// <summary>Changes to try together on a board, in order. None of them is saved.</summary>
public sealed record BoardWhatIfRequest(IReadOnlyCollection<BoardWhatIfChangeRequest>? Changes);

/// <summary>
/// A change to try. <c>SET_SIZE</c> takes a <paramref name="Size"/> (null
/// clears it), <c>ASSIGN</c> the <paramref name="UserIds"/> who take the card
/// instead of its assignees, <c>ADD_DEPENDENCY</c> and <c>REMOVE_DEPENDENCY</c>
/// a <paramref name="DependsOnCardId"/>, <c>SET_TARGET</c> a
/// <paramref name="Date"/>, <c>ADD_PEOPLE</c> a <paramref name="Count"/>;
/// <c>COMPLETE</c> takes a card alone.
/// </summary>
public sealed record BoardWhatIfChangeRequest(
  [property: EnumCodes<WhatIfChangeKind>] string? Kind,
  Guid? CardId,
  string? Size,
  IReadOnlyCollection<Guid>? UserIds,
  Guid? DependsOnCardId,
  DateOnly? Date,
  int? Count);

/// <summary>The board's milestones as they are, and as they would be with the changes.</summary>
public sealed record BoardWhatIfResponse(
  IReadOnlyCollection<BoardMilestoneResponse> Before,
  IReadOnlyCollection<BoardMilestoneResponse> After);
