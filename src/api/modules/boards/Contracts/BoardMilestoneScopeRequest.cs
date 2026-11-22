namespace Coordina.Api.Modules.Boards.Contracts;

/// <summary>Cards the proposal must keep, with everything they need.</summary>
public sealed record BoardMilestoneScopeRequest(IReadOnlyCollection<Guid>? Keep);

/// <summary>The cards to take out of a milestone, as a proposal lists them.</summary>
public sealed record BoardMilestoneCutRequest(IReadOnlyCollection<Guid>? CardIds);
