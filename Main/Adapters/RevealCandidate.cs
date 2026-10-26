namespace TAOM.Adapters;

/// <summary>
/// A town or castle the map reveal could mark: its id, its straight-line distance on the campaign map from the settlement
/// asked about, and whether it already carries a tracker mark.
/// </summary>
public sealed record RevealCandidate(string SettlementId, float Distance, bool IsTracked);
