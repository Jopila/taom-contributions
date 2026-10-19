namespace TAOM.Features.Relations;

/// <summary>
/// The personal-relations rules <c>TaomDiplomacyModel</c> asks for. Engine-free: the model converts at its boundary and the
/// notice patch asks the same question.
/// </summary>
public interface IPersonalRelationService
{
    /// <summary>
    /// True when personal relations apply right now: the seeding behavior has engaged the service for this campaign (a single
    /// player campaign whose save is seeded) and the MCM switch is on. One volatile bool and one cached settings read, so it
    /// is safe to ask on every relation read. Never read co-op state here: those reads take locks or use reflection.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Arms or disarms the service. Only <c>PersonalRelationSeedBehavior</c> calls it: disarmed at each campaign's start and on
    /// any fault, armed once the save is seeded in a single player campaign. Disarmed, the whole feature is vanilla.
    /// </summary>
    void Engage(bool engaged);

    /// <summary>
    /// Which effective hero of a relation pair goes back to the hero it was asked about, so the player's relation stays on
    /// the lord dealt with instead of his clan leader. Only when <see cref="IsActive"/> and exactly one effective hero is the
    /// player; every AI-to-AI pair is vanilla.
    /// </summary>
    PersonalPairSide Resolve(bool effective1IsPlayer, bool effective2IsPlayer);

    /// <summary>
    /// The relation change (a negative number) for disbanding an army by paying influence, or null for "vanilla decides": the
    /// service is inactive, the disband cost is off, or the party is a member (vanilla's -1 stays). Only the army leader rolls.
    /// </summary>
    int? DisbandCost(bool isLeaderParty);

    /// <summary>
    /// The relation a living hero inherits when a clan leader dies and a new one is chosen: 0 for the player while
    /// <see cref="IsActive"/> (the heir keeps his own relation), else null for "vanilla decides".
    /// </summary>
    int? InheritedRelationChange(bool relationHeroIsPlayer);
}
