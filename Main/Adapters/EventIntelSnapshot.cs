namespace TAOM.Adapters;

/// <summary>
/// Plain facts about one map event or siege as the player's main party sees it (#846 phase 2, built by
/// <see cref="IPartyIntelAdapter"/>). <c>default</c> means the campaign or the event was missing; <see cref="HasData"/> is
/// then false.
/// </summary>
public readonly struct EventIntelSnapshot
{
    public static EventIntelSnapshot NoData => default;

    public bool HasData { get; init; }

    /// <summary>The healthy troops of every involved party that is not a settlement, both sides together.</summary>
    public int TotalHealthy { get; init; }

    /// <summary>Map distance from the main party to the event (a siege is placed at the besieged settlement's gate).</summary>
    public float DistanceToMain { get; init; }

    /// <summary>The main party's seeing range, in map units.</summary>
    public float MainSeeingRange { get; init; }

    /// <summary>The Scouting skill of the main party's scout, 0 when it has none.</summary>
    public int ScoutingSkill { get; init; }

    /// <summary>The main party is in the event, or an involved party is of the player's clan or army.</summary>
    public bool PlayerInvolved { get; init; }

    /// <summary>Either side's faction is the main party's.</summary>
    public bool SameFaction { get; init; }

    /// <summary><c>Campaign.TrueSight</c>: the map shows everything.</summary>
    public bool TrueSight { get; init; }
}
