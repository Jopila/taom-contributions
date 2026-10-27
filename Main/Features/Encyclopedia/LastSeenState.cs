using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.Encyclopedia;

/// <summary>
/// The hero state that vanilla v1.5.5 <c>HeroHelper.GetLastSeenText</c> branches on, captured after the
/// call threw. <see cref="ReachesUnguardedLeaderRead"/> says whether that state reaches one of the
/// method's unguarded reads, so Patch123 contains only the defect it exists for.
/// </summary>
public sealed class LastSeenState
{
    public LastSeenState(
        string heroId,
        bool knownAndAlive,
        bool hasLastSeen,
        bool isPrisoner,
        bool hasParty,
        string? partyId,
        bool partyIsMobile,
        bool partyLeaderMissing,
        bool hasArmy,
        bool armyLeaderMissing,
        TextObject? settlementLink,
        bool isInSettlement)
    {
        HeroId = heroId;
        KnownAndAlive = knownAndAlive;
        HasLastSeen = hasLastSeen;
        IsPrisoner = isPrisoner;
        HasParty = hasParty;
        PartyId = partyId;
        PartyIsMobile = partyIsMobile;
        PartyLeaderMissing = partyLeaderMissing;
        HasArmy = hasArmy;
        ArmyLeaderMissing = armyLeaderMissing;
        SettlementLink = settlementLink;
        IsInSettlement = isInSettlement;
    }

    public string HeroId { get; }
    public bool KnownAndAlive { get; }
    public bool HasLastSeen { get; }
    public bool IsPrisoner { get; }
    public bool HasParty { get; }
    public string? PartyId { get; }
    public bool PartyIsMobile { get; }
    public bool PartyLeaderMissing { get; }
    public bool HasArmy { get; }
    public bool ArmyLeaderMissing { get; }
    public TextObject? SettlementLink { get; }
    public bool IsInSettlement { get; }

    /// <summary>
    /// True when vanilla reads a missing leader hero (v1.5.5 HeroHelper.cs). Dead, fugitive, unknown,
    /// never-seen and partyless heroes return before any read (:22-40). A prisoner of a mobile party
    /// reads the army leader in an army (:53, :80), else the captor's leader (:58, :86), at sea or on
    /// land alike; a prisoner in a settlement reads none (:91-93). Anyone else reads the army leader in
    /// an army (:65, :98), and a settlement party's <c>MobileParty.Army</c> (:95).
    /// </summary>
    public bool ReachesUnguardedLeaderRead
    {
        get
        {
            if (!KnownAndAlive || !HasLastSeen || !HasParty) return false;
            if (IsPrisoner) return PartyIsMobile && (HasArmy ? ArmyLeaderMissing : PartyLeaderMissing);
            return !PartyIsMobile || (HasArmy && ArmyLeaderMissing);
        }
    }

    /// <summary>Engine boundary: reads the hero. A failed read gives a state that contains nothing.</summary>
    public static LastSeenState Capture(Hero? hero)
    {
        var heroId = "<null>";
        try
        {
            if (hero == null) return Unreadable(heroId);
            heroId = hero.StringId ?? "<null>";
            var party = hero.PartyBelongedTo?.Party ?? hero.PartyBelongedToAsPrisoner;
            var army = party?.MobileParty?.Army;
            var settlement = hero.LastKnownClosestSettlement;
            return new LastSeenState(
                heroId,
                knownAndAlive: !hero.IsDead && !hero.IsFugitive && hero.IsKnownToPlayer,
                hasLastSeen: settlement != null,
                isPrisoner: hero.IsPrisoner,
                hasParty: party != null,
                partyId: party?.Id,
                partyIsMobile: party?.IsMobile == true,
                partyLeaderMissing: party?.LeaderHero == null,
                hasArmy: army != null,
                armyLeaderMissing: army != null && army.LeaderParty?.LeaderHero == null,
                settlementLink: settlement?.EncyclopediaLinkWithName,
                isInSettlement: settlement != null && settlement == hero.CurrentSettlement);
        }
        catch
        {
            return Unreadable(heroId);
        }
    }

    private static LastSeenState Unreadable(string heroId) => new(
        heroId, false, false, false, false, null, false, false, false, false, null, false);
}
