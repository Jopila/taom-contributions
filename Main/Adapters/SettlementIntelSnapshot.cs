namespace TAOM.Adapters;

/// <summary>
/// Plain facts about one settlement as the player's main party sees it (#846 phase 2, built by
/// <see cref="IPartyIntelAdapter"/>). The adapter reports what the campaign says; the service decides what follows.
/// <c>default</c> means the campaign or the settlement was missing; <see cref="HasData"/> is then false.
/// </summary>
public readonly struct SettlementIntelSnapshot
{
    public static SettlementIntelSnapshot NoData => default;

    public bool HasData { get; init; }

    public string? StringId { get; init; }

    /// <summary>
    /// The healthy troops the vanilla settlement tooltip prints as its defenders: parties inside that are of the settlement's
    /// faction and are lord parties, garrisons, or militia while the town is not rebellious.
    /// </summary>
    public int DefenderCount { get; init; }

    /// <summary>Map distance from the main party to the settlement's gate (vanilla inspects a settlement by its gate).</summary>
    public float DistanceToMain { get; init; }

    /// <summary>The main party's seeing range, in map units.</summary>
    public float MainSeeingRange { get; init; }

    /// <summary>The Scouting skill of the main party's scout, 0 when it has none.</summary>
    public int ScoutingSkill { get; init; }

    /// <summary>The main party is inside this settlement.</summary>
    public bool IsPlayerSettlement { get; init; }

    /// <summary>The player's clan owns the settlement.</summary>
    public bool OwnedByPlayerClan { get; init; }

    /// <summary>A hero of the player's clan is currently in the settlement (TAOM's informant rule: the player's own people inside tell it the garrison).</summary>
    public bool PlayerClanHeroInside { get; init; }

    /// <summary>The settlement's map faction is the main party's.</summary>
    public bool SameFaction { get; init; }

    /// <summary>The campaign clock in whole hours, or -1 when it is unusable.</summary>
    public int NowHour { get; init; }

    /// <summary><c>Campaign.TrueSight</c>: the map shows everything.</summary>
    public bool TrueSight { get; init; }
}
