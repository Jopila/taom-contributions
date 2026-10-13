namespace TAOM.Adapters;

/// <summary>
/// What the Eye of Sauron hunt (#765 M8) needs to know about a candidate hunter, read from the live hero on every use.
/// Plain values only, so the service stays free of engine types (ADR-007).
/// </summary>
public sealed class HunterHeroSnapshot
{
    public static readonly HunterHeroSnapshot Missing = new();

    public bool Exists { get; set; }

    public bool IsAlive { get; set; }

    /// <summary><c>Hero.IsActive</c>: the hero is in play, not disabled or not yet born.</summary>
    public bool IsActive { get; set; }

    public bool IsFugitive { get; set; }

    public bool IsPrisoner { get; set; }

    /// <summary><c>PartyBelongedToAsPrisoner != null</c>.</summary>
    public bool IsHeldByAParty { get; set; }

    /// <summary><c>DeathMark != None</c>.</summary>
    public bool IsMarkedForDeath { get; set; }

    /// <summary>His live <c>MapFaction</c> StringId, or null.</summary>
    public string? FactionId { get; set; }

    /// <summary>The live faction is at war with the player's kingdom (<c>FactionManager.IsAtWarAgainstFaction</c>).</summary>
    public bool AtWarWithPlayerKingdom { get; set; }

    /// <summary><c>PartyBelongedTo != null</c>, led by him or not.</summary>
    public bool IsInAParty { get; set; }

    /// <summary>The StringId of the party he LEADS, or null when he leads none.</summary>
    public string? LedPartyId { get; set; }

    public bool CanLeadParty { get; set; }

    /// <summary><c>CharacterObject.Occupation == Lord</c>.</summary>
    public bool IsLord { get; set; }

    /// <summary>His settlement holds a tournament or is under siege (vanilla's spawn guard, HeroSpawnCampaignBehavior:163).</summary>
    public bool SettlementBlocksSpawn { get; set; }
}

/// <summary>The hunter's party, read from the live <c>MobileParty</c> on every use.</summary>
public sealed class HuntPartySnapshot
{
    public static readonly HuntPartySnapshot Missing = new();

    public bool Exists { get; set; }

    public bool IsActive { get; set; }

    public string? LeaderHeroId { get; set; }

    public bool InMapEvent { get; set; }

    /// <summary><c>Army != null</c> (leading one or attached to one).</summary>
    public bool InArmy { get; set; }

    /// <summary>Besieging a settlement (a siege camp).</summary>
    public bool Besieging { get; set; }

    public bool InSettlement { get; set; }

    public bool IsDisbanding { get; set; }

    /// <summary><c>Ai.DoNotMakeNewDecisions</c>, whoever set it.</summary>
    public bool AiPinned { get; set; }

    /// <summary>Its default behaviour is to engage the player's party.</summary>
    public bool IsEngagingPlayer { get; set; }

    public int HeadCount { get; set; }

    /// <summary><c>Party.EstimatedStrength</c>.</summary>
    public float Strength { get; set; }

    public int Food { get; set; }
}

/// <summary>The inputs of the in-sight test, read on the main thread every frame while a hunt runs.</summary>
public sealed class HuntSightReading
{
    public static readonly HuntSightReading Missing = new();

    public bool Valid { get; set; }

    public bool HunterActive { get; set; }

    public bool PlayerActive { get; set; }

    public bool HunterInMapEvent { get; set; }

    public bool PlayerInMapEvent { get; set; }

    public bool PlayerAtSea { get; set; }

    /// <summary>The player's seeing range, S.</summary>
    public float SeeingRange { get; set; }

    /// <summary>Straight-line distance from the hunter to the player.</summary>
    public float Distance { get; set; }

    /// <summary><c>MobileParty.MainParty.Speed</c>.</summary>
    public float PlayerSpeed { get; set; }
}

/// <summary>
/// Everything one hourly relocation step needs. <see cref="BlockedBy"/> names the first engine guard that holds (the
/// hunter or the player is busy, at sea, visible, on another island, or the path rebuild failed); the service steps
/// only when it is null. The path runs from the hunter through <see cref="PathX"/>/<see cref="PathY"/> to the player.
/// </summary>
public sealed class HuntStepContext
{
    public string? BlockedBy { get; set; }

    public float SeeingRange { get; set; }

    public float HunterX { get; set; }

    public float HunterY { get; set; }

    public float PlayerX { get; set; }

    public float PlayerY { get; set; }

    public float[] PathX { get; set; } = new float[0];

    public float[] PathY { get; set; } = new float[0];

    public static HuntStepContext Blocked(string reason) => new() { BlockedBy = reason };
}

/// <summary>A landing point the adapter accepted (valid, on the player's island, no enemy of the hunter nearby).</summary>
public readonly struct HuntLanding
{
    public HuntLanding(bool ok, float x, float y)
    {
        Ok = ok;
        X = x;
        Y = y;
    }

    public bool Ok { get; }

    public float X { get; }

    public float Y { get; }
}

/// <summary>The four lines the hunt shows the player (#765 decision: once at the start, once at the end).</summary>
public enum HuntAnnouncement
{
    /// <summary>A Free-side player: the Eye turns, a Nazgul rides.</summary>
    StartFree,

    /// <summary>An Evil-side player: Elrond sends Glorfindel.</summary>
    StartEvil,

    /// <summary>The hunter was beaten by the player.</summary>
    Won,

    /// <summary>Any other end.</summary>
    Over,
}
