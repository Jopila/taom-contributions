namespace TAOM.Features.InnRounds.Domain;

/// <summary>
/// The cheap facts about the conversation partner and the town the player stands in, taken at the engine boundary and safe to
/// read in a dialog condition. Ids and plain values only (ADR-007).
/// </summary>
public sealed class TavernSnapshot
{
    /// <summary>True when the conversation partner is a tavern keeper (<c>Occupation.Tavernkeeper</c>).</summary>
    public bool IsTavernkeeper { get; set; }

    /// <summary>True when the player stands in a town (not a castle or a village).</summary>
    public bool InTown { get; set; }

    public string SettlementId { get; set; } = string.Empty;

    /// <summary>The town's culture id; empty when it has none.</summary>
    public string CultureId { get; set; } = string.Empty;

    /// <summary>True when the town's map faction is at war with the player's.</summary>
    public bool AtWarWithPlayer { get; set; }

    /// <summary>The player's relation with the leader of the clan that owns the town; 0 when it has no owner.</summary>
    public float Relation { get; set; }
}
