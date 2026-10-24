namespace TAOM.Features.RedArrow;

/// <summary>
/// One party the player might call into the army, as <c>IArmyCallCandidateAdapter</c> read it. Primitives only (ADR-007).
/// The first seven facts are what vanilla's checks 2 to 8 read; the last five are what checks 9 to 12 read (sea, raft and
/// distance). The adapter fills a fact vanilla would not have read with its refusing value (no naval capability, NaN distance).
/// </summary>
public readonly struct ArmyCallCandidate
{
    public ArmyCallCandidate(
        bool hasParty,
        bool isFactionLeaderParty,
        CandidateArmy army,
        bool isFightingOrBesieged,
        float sizeScore,
        float minSizeScore,
        bool isDisbanding,
        bool mainPartyAtSea,
        bool hasNavalCapability,
        bool isInRaftState,
        float distance,
        float maxDistance)
    {
        HasParty = hasParty;
        IsFactionLeaderParty = isFactionLeaderParty;
        Army = army;
        IsFightingOrBesieged = isFightingOrBesieged;
        SizeScore = sizeScore;
        MinSizeScore = minSizeScore;
        IsDisbanding = isDisbanding;
        MainPartyAtSea = mainPartyAtSea;
        HasNavalCapability = hasNavalCapability;
        IsInRaftState = isInRaftState;
        Distance = distance;
        MaxDistance = maxDistance;
    }

    /// <summary>The model was asked about a real party (vanilla: <c>party != null</c>).</summary>
    public bool HasParty { get; }

    /// <summary>
    /// <c>party.LeaderHero == Hero.MainHero.MapFaction?.Leader</c>, evaluated as vanilla does, so two nulls are equal and a
    /// leaderless party reads as the ruler's when the player's faction has no leader.
    /// </summary>
    public bool IsFactionLeaderParty { get; }

    /// <summary>The candidate's army against the player's.</summary>
    public CandidateArmy Army { get; }

    /// <summary>The party has a map event or a siege event, or stands in a settlement that is under siege.</summary>
    public bool IsFightingOrBesieged { get; }

    /// <summary>The model's <c>GetPartySizeScore(party)</c>: the party size ratio, at most 1.</summary>
    public float SizeScore { get; }

    /// <summary>The model's <c>PlayerMobilePartySizeRatioToCallToArmy</c> (0.4 in vanilla).</summary>
    public float MinSizeScore { get; }

    /// <summary>The party is disbanding, or waits for the disband behavior to remove it.</summary>
    public bool IsDisbanding { get; }

    /// <summary>The player's party is at sea.</summary>
    public bool MainPartyAtSea { get; }

    /// <summary>The candidate can travel by sea (<c>MobileParty.HasNavalNavigationCapability</c>); read only while the player is at sea.</summary>
    public bool HasNavalCapability { get; }

    /// <summary>The candidate is lost at sea on a raft.</summary>
    public bool IsInRaftState { get; }

    /// <summary>Map distance from the candidate to the player's party; NaN when vanilla would not measure it (at sea or on a raft).</summary>
    public float Distance { get; }

    /// <summary>The model's <c>MaximumDistanceToCallToArmy</c>.</summary>
    public float MaxDistance { get; }
}
