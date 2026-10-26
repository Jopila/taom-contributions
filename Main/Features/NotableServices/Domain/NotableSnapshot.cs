using TAOM.Features.Execution;

namespace TAOM.Features.NotableServices.Domain;

/// <summary>
/// The cheap facts about the notable in the conversation, taken at the engine boundary and safe to read in a dialog
/// condition. Ids and plain values only (ADR-007).
/// </summary>
public sealed class NotableSnapshot
{
    public NotableSnapshot(string heroId)
    {
        HeroId = heroId;
    }

    public string HeroId { get; }

    /// <summary>Hero.IsNotable, which is also true for a preacher.</summary>
    public bool IsNotable { get; set; }

    public bool IsPreacher { get; set; }

    /// <summary>True when the notable stands in the settlement the main hero is in now.</summary>
    public bool InPlayersSettlement { get; set; }

    public string SettlementId { get; set; } = string.Empty;

    public SettlementKind Kind { get; set; } = SettlementKind.Other;

    /// <summary>The settlement's map faction id with that faction's culture: the pair the alignment table resolves.</summary>
    public SideIds SettlementSide { get; set; } = SideIds.None;

    /// <summary>True when the settlement's map faction is at war with the player's.</summary>
    public bool AtWarWithPlayer { get; set; }

    /// <summary>The player's relation with the notable, as the engine reports it.</summary>
    public float Relation { get; set; }
}
