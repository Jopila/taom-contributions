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

    /// <summary>The healthy troops of the party's own roster, attached parties not counted (the row of a settlement tooltip prints this).</summary>
    public int OwnHealthyCount { get; init; }

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

    /// <summary><c>MobileParty.IsVisible</c> (a saved field, read only): the party is drawn on the map now.</summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// The party is next to the player and already exact by distance: in the main party's current settlement, the party the
    /// player is encountering or talking to, or in the main party's map event or siege. Never masked.
    /// </summary>
    public bool InPlainSight { get; init; }

    /// <summary>The party leads an army, so <see cref="HealthyCount"/> is the army's total and <see cref="OwnHealthyCount"/> its own roster.</summary>
    public bool LeadsArmy { get; init; }

    /// <summary>The party's map faction is at war with the main party's (<c>FactionManager.IsAtWarAgainstFaction</c>). Reading an enemy host trains the scout.</summary>
    public bool AtWar { get; init; }

    /// <summary>The campaign clock in whole hours, or -1 when it is unusable (no campaign, not finite). Read live on every call (one property read), so a stamp is never an hour behind.</summary>
    public int NowHour { get; init; }

    /// <summary>The engine already hides this party's numbers (<c>MobileParty.IsInfoHidden</c>, a saved field TAOM only reads).</summary>
    public bool IsInfoHidden { get; init; }
}
