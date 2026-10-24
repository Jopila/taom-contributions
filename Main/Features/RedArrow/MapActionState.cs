namespace TAOM.Features.RedArrow;

/// <summary>
/// The facts <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c> reads (v1.5.5), read by
/// <c>IPlayerSiegeStateAdapter</c>. The four encounter facts mean something only while <see cref="HasEncounter"/> is set,
/// as in vanilla. Primitives only (ADR-007).
/// </summary>
public readonly struct MapActionState
{
    public MapActionState(
        bool isPrisoner,
        bool isInRaftState,
        bool isInFerryState,
        bool inMission,
        bool hasEncounter,
        bool encounterHasSettlement,
        bool raidAtEncounter,
        bool encounterSettlementUnderSiege,
        bool encounterSettlementIsOwnSiege,
        bool hasMapEvent)
    {
        IsPrisoner = isPrisoner;
        IsInRaftState = isInRaftState;
        IsInFerryState = isInFerryState;
        InMission = inMission;
        HasEncounter = hasEncounter;
        EncounterHasSettlement = encounterHasSettlement;
        RaidAtEncounter = raidAtEncounter;
        EncounterSettlementUnderSiege = encounterSettlementUnderSiege;
        EncounterSettlementIsOwnSiege = encounterSettlementIsOwnSiege;
        HasMapEvent = hasMapEvent;
    }

    public bool IsPrisoner { get; }

    public bool IsInRaftState { get; }

    public bool IsInFerryState { get; }

    /// <summary>A campaign mission is running (<c>CampaignMission.Current != null</c>).</summary>
    public bool InMission { get; }

    /// <summary>The player has an encounter (<c>PlayerEncounter.Current != null</c>).</summary>
    public bool HasEncounter { get; }

    /// <summary>The encounter has a settlement (<c>PlayerEncounter.EncounterSettlement != null</c>).</summary>
    public bool EncounterHasSettlement { get; }

    /// <summary>The encounter's village is being raided and the player's party is in that raid's map event.</summary>
    public bool RaidAtEncounter { get; }

    /// <summary>The encounter's settlement is under siege.</summary>
    public bool EncounterSettlementUnderSiege { get; }

    /// <summary>The encounter's settlement is the one the player's own besieger camp is besieging.</summary>
    public bool EncounterSettlementIsOwnSiege { get; }

    /// <summary>The player's party is in a map event.</summary>
    public bool HasMapEvent { get; }
}
