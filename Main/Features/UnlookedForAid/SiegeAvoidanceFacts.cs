namespace TAOM.Features.UnlookedForAid;

/// <summary>
/// What the service needs to know about one "should this lord avoid that party" question, read by
/// <c>SiegeAvoidanceFactsReader</c> (AI worker thread or main thread). Primitives only (ADR-007): no engine type reaches
/// the service.
/// </summary>
public readonly struct SiegeAvoidanceFacts
{
    public SiegeAvoidanceFacts(
        bool isLordParty,
        bool targetIsBesieger,
        bool targetInBesiegedSettlement,
        bool playerLedSiege,
        float ownStrength,
        float campStrength)
    {
        IsLordParty = isLordParty;
        TargetIsBesieger = targetIsBesieger;
        TargetInBesiegedSettlement = targetInBesiegedSettlement;
        PlayerLedSiege = playerLedSiege;
        OwnStrength = ownStrength;
        CampStrength = campStrength;
    }

    /// <summary>The evaluating party is a lord party (caravans, villagers, bandits, patrols and garrisons are not).</summary>
    public bool IsLordParty { get; }

    /// <summary>The target belongs to a besieger camp.</summary>
    public bool TargetIsBesieger { get; }

    /// <summary>The target stands inside a settlement under siege.</summary>
    public bool TargetInBesiegedSettlement { get; }

    /// <summary>
    /// The siege involved (the target's besieger camp, or the camp besieging the settlement the target stands in) has the
    /// player's party as its camp leader. False when the evaluator is not a lord party.
    /// </summary>
    public bool PlayerLedSiege { get; }

    /// <summary>The evaluating lord's strength: its army's when it leads that army or is attached to it, else its own party's. NaN when not read.</summary>
    public float OwnStrength { get; }

    /// <summary>The sum of the besieger camp's parties' strength. NaN unless the target is a besieger and the evaluator a lord party, or when the read failed.</summary>
    public float CampStrength { get; }
}
