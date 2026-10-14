namespace TAOM.Features.EyeOfSauron;

/// <summary>Why a hunt ended. Every reason releases the AI pin the hunt set.</summary>
public enum HuntEndReason
{
    /// <summary>The player beat the hunter's party in battle.</summary>
    HunterBeaten,

    /// <summary>The hunter's party fought any other battle (the player's flee included), or lost to someone else.</summary>
    BattleEnded,

    /// <summary>The party was destroyed, cannot be found, or is no longer led by the hunter.</summary>
    PartyLost,

    /// <summary>The hunter died, was captured or became a fugitive.</summary>
    HunterDown,

    /// <summary>The player's kingdom is no longer at war with the hunter's faction.</summary>
    WarEnded,

    /// <summary>The player left his kingdom.</summary>
    Kingdomless,

    /// <summary>A co-op session, a co-op mod TAOM cannot probe, or a dedicated server went live.</summary>
    SessionLive,

    /// <summary>HuntTimeoutDays passed.</summary>
    TimedOut,

    /// <summary>The MCM switch was turned off.</summary>
    Disabled,

    /// <summary><c>taom.clear_eye_hunt</c>.</summary>
    Manual,
}

/// <summary>The battle types a deed is weighted by (MapEvent.cs:197-215).</summary>
public enum DeedKind
{
    /// <summary>
    /// Weight 1: a field battle, or any other kind the player wins against an enemy kingdom, a village raid and a
    /// forced levy of supplies or volunteers included.
    /// </summary>
    Field,

    /// <summary>Weight 2: a sally out or a battle outside a siege.</summary>
    SallyOrOutside,

    /// <summary>Weight 3: a siege assault.</summary>
    Assault,
}

/// <summary>A finished map event, reduced to what the hunt reads (built on the main thread at MapEventEnded).</summary>
public sealed class HuntBattleSummary
{
    public bool IsPlayerEvent { get; set; }

    /// <summary><c>SpecialResourceEarnPolicy.IsPlayerVictory(PlayerSide, WinningSide)</c>.</summary>
    public bool PlayerWon { get; set; }

    /// <summary>The losing side's faction StringId when that faction is a Kingdom, else null.</summary>
    public string? LoserKingdomId { get; set; }

    public DeedKind Kind { get; set; }

    /// <summary>The hunter's party took part.</summary>
    public bool InvolvesHunter { get; set; }
}

/// <summary>
/// The hunt's saved record: StringIds and primitives only, resolved against the campaign on every use. Public fields,
/// so the behavior's SyncData takes each one by ref; an empty id means no hunt.
/// </summary>
public sealed class EyeOfSauronState
{
    public float ScoreVsEvil;

    public float ScoreVsFree;

    public float LastDecayDay;

    public float CooldownUntilDay;

    public string HunterHeroId = string.Empty;

    public string HunterPartyId = string.Empty;

    public float HuntStartDay;
}
