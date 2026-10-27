using Helpers;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Encyclopedia;

/// <summary>
/// The hero state that vanilla v1.5.5 <c>HeroHelper.GetLastSeenText</c> branches on.
/// <see cref="ReachesUnguardedLeaderRead"/> says whether vanilla would read a missing leader hero, so
/// Patch123 answers only that case and leaves every other call to vanilla.
/// </summary>
public sealed class LastSeenState
{
    public LastSeenState(
        string heroId,
        bool knownAndAlive,
        bool hasLastSeen,
        bool isInSettlement,
        bool isPrisoner,
        bool hasParty,
        string? partyId,
        bool partyIsMobile,
        bool partyLeaderMissing,
        bool hasArmy,
        bool armyLeaderMissing)
    {
        HeroId = heroId;
        KnownAndAlive = knownAndAlive;
        HasLastSeen = hasLastSeen;
        IsInSettlement = isInSettlement;
        IsPrisoner = isPrisoner;
        HasParty = hasParty;
        PartyId = partyId;
        PartyIsMobile = partyIsMobile;
        PartyLeaderMissing = partyLeaderMissing;
        HasArmy = hasArmy;
        ArmyLeaderMissing = armyLeaderMissing;
    }

    public string HeroId { get; }
    public bool KnownAndAlive { get; }
    public bool HasLastSeen { get; }
    public bool IsInSettlement { get; }
    public bool IsPrisoner { get; }
    public bool HasParty { get; }
    public string? PartyId { get; }
    public bool PartyIsMobile { get; }
    public bool PartyLeaderMissing { get; }
    public bool HasArmy { get; }
    public bool ArmyLeaderMissing { get; }

    /// <summary>
    /// True when the party structure leads vanilla to a missing leader hero (v1.5.5 HeroHelper.cs).
    /// A prisoner of a mobile party reads the army leader in an army (:53, :80), else the captor's
    /// leader (:58, :86), at sea or on land alike; a prisoner in a settlement reads none (:91-93).
    /// Anyone else reads the army leader in an army (:65, :98), and a settlement party's
    /// <c>MobileParty.Army</c> (:95). A hero with no party reads none (:38-45).
    /// </summary>
    public bool PartyReachesUnguardedRead
    {
        get
        {
            if (!HasParty) return false;
            if (IsPrisoner) return PartyIsMobile && (HasArmy ? ArmyLeaderMissing : PartyLeaderMissing);
            return !PartyIsMobile || (HasArmy && ArmyLeaderMissing);
        }
    }

    /// <summary>
    /// <see cref="PartyReachesUnguardedRead"/>, after vanilla's early returns: dead, fugitive, unknown
    /// and never-seen heroes return before any read (:22-37).
    /// </summary>
    public bool ReachesUnguardedLeaderRead => KnownAndAlive && HasLastSeen && PartyReachesUnguardedRead;

    /// <summary>
    /// Engine boundary: reads the hero. Only when the party structure reaches a missing leader does it
    /// repeat vanilla's refresh of the last-known settlement (:30-33), which vanilla runs before its
    /// never-seen return; every other hero is left untouched for vanilla. A failed read leaves the call
    /// to vanilla.
    /// </summary>
    public static LastSeenState Capture(Hero? hero)
    {
        var heroId = "<null>";
        try
        {
            if (hero == null) return Unreadable(heroId);
            heroId = hero.StringId ?? "<null>";
            var knownAndAlive = !hero.IsDead && !hero.IsFugitive && hero.IsKnownToPlayer;
            var party = hero.PartyBelongedTo?.Party ?? hero.PartyBelongedToAsPrisoner;
            var army = party?.MobileParty?.Army;
            var hasParty = party != null;
            var partyIsMobile = party?.IsMobile == true;
            var partyLeaderMissing = party?.LeaderHero == null;
            var hasArmy = army != null;
            var armyLeaderMissing = army != null && army.LeaderParty?.LeaderHero == null;

            var structure = new LastSeenState(heroId, knownAndAlive, false, false, hero.IsPrisoner, hasParty,
                party?.Id, partyIsMobile, partyLeaderMissing, hasArmy, armyLeaderMissing);
            if (!knownAndAlive || !structure.PartyReachesUnguardedRead) return structure;

            if (hero.IsActive || hero.IsPrisoner)
                hero.UpdateLastKnownClosestSettlement(HeroHelper.GetClosestSettlement(hero));
            var settlement = hero.LastKnownClosestSettlement;
            return new LastSeenState(heroId, knownAndAlive, settlement != null,
                settlement != null && settlement == hero.CurrentSettlement, hero.IsPrisoner, hasParty,
                party?.Id, partyIsMobile, partyLeaderMissing, hasArmy, armyLeaderMissing);
        }
        catch
        {
            return Unreadable(heroId);
        }
    }

    private static LastSeenState Unreadable(string heroId) =>
        new(heroId, false, false, false, false, false, null, false, false, false, false);
}
