namespace TAOM.Features.NotableServices.Domain;

/// <summary>
/// A request to point out the holds near a settlement: the settlement the keeper or notable speaks for, whether its map faction
/// is at war with the player's, and the player's relation with the one who answers.
/// </summary>
public sealed record RevealAsk(string SettlementId, bool AtWarWithPlayer, float Relation);
