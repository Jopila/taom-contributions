namespace TAOM.Adapters;

/// <summary>
/// Plain facts about one party as the player's main party sees it (#846, built by <see cref="IPartyIntelAdapter"/>). The
/// adapter reports what the campaign says; the service decides what follows from it (who is exempt, who counts as the
/// same realm). <c>default</c> and <see cref="NoData"/> mean the campaign or a party was missing; <see cref="HasData"/> is
/// then false.
/// </summary>
public readonly struct PartyIntelSnapshot
{
    public static PartyIntelSnapshot NoData => default;

    public bool HasData { get; init; }

    public string? StringId { get; init; }

    /// <summary>The healthy troops the vanilla nameplate would show (an army leader counts its attached parties).</summary>
    public int HealthyCount { get; init; }

    /// <summary>Map distance from the main party.</summary>
    public float DistanceToMain { get; init; }

    /// <summary>The main party's seeing range, in map units.</summary>
    public float MainSeeingRange { get; init; }

    /// <summary>
    /// The engine's spotting ratio for this party: it is seen out to this fraction of the seeing range. A party that
    /// besieges or raids a settlement the player can see stays visible farther out than this ratio says.
    /// </summary>
    public float SpottingRatio { get; init; }

    /// <summary>The Scouting skill of the main party's scout, 0 when it has none.</summary>
    public int ScoutingSkill { get; init; }

    /// <summary>The party is the main party itself.</summary>
    public bool IsMainParty { get; init; }

    /// <summary>The party belongs to the player's clan.</summary>
    public bool InPlayerClan { get; init; }

    /// <summary>The party is in the main party's army.</summary>
    public bool InPlayerArmy { get; init; }

    /// <summary><c>Campaign.TrueSight</c>: the map shows every party.</summary>
    public bool TrueSight { get; init; }

    /// <summary>The party's map faction is the main party's.</summary>
    public bool SameFaction { get; init; }

    /// <summary>The party's army leader's map faction is the main party's.</summary>
    public bool ArmyLeaderSameFaction { get; init; }

    /// <summary>The engine already hides this party's numbers (<c>MobileParty.IsInfoHidden</c>, a saved field TAOM only reads).</summary>
    public bool IsInfoHidden { get; init; }
}
