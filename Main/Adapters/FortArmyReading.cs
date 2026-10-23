using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// One AI or player army leader as Hold the Walls reads it (#871): plain values for a decision, no engine object. A
/// leader the engine no longer knows as the leader of an army is <see cref="Missing"/>.
/// </summary>
public sealed class FortArmyReading
{
    public static readonly FortArmyReading Missing = new FortArmyReading();

    /// <summary>The party exists, is active and still leads an army.</summary>
    public bool Exists { get; set; }

    /// <summary>The leader party's StringId.</summary>
    public string LeaderPartyId { get; set; } = string.Empty;

    /// <summary>The leader stands inside the fortress asked about.</summary>
    public bool InFort { get; set; }

    /// <summary>The leader's map faction is the fortress's map faction, so the army belongs to the realm that holds it.</summary>
    public bool SameRealmAsFort { get; set; }

    /// <summary>
    /// The leader's AI is paused (<c>MobilePartyAi.IsDisabled</c>). The engine pauses only the leaders that took the
    /// fortress, so a same-realm army that walked in afterwards reads false and is never held. An army that fled into the fortress just
    /// before the first pass also reads true (<c>EnterSettlementAction</c> pauses a fleeing party for 5 hours) and may be held; accepted.
    /// </summary>
    public bool AiPaused { get; set; }

    /// <summary>The player leads the army or has joined it. Such an army is never held.</summary>
    public bool IsPlayerLed { get; set; }

    public bool InMapEvent { get; set; }

    public bool FortUnderSiege { get; set; }

    /// <summary>The army's cohesion, 0 to 100; NaN when unreadable.</summary>
    public float Cohesion { get; set; } = float.NaN;

    /// <summary>
    /// Days of food in the army's leanest party (the leader or any attached party), as the engine's own formula counts them
    /// but as a fraction; positive infinity when no party consumes food; NaN when unreadable.
    /// </summary>
    public float LowestFoodDays { get; set; } = float.NaN;

    /// <summary>The leader hero's Valor level; 0 when the leader has no hero.</summary>
    public int Valor { get; set; }

    /// <summary>The leader hero's Calculating level; 0 when the leader has no hero.</summary>
    public int Calculating { get; set; }

    /// <summary>The id and culture of the leader's realm, for <c>IAlignmentService.ResolveSide</c>.</summary>
    public SideIds Side { get; set; } = SideIds.None;
}
