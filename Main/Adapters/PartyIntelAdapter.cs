using System.Collections.Generic;
using System.Diagnostics;
using Helpers;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TAOM.Features.HiddenHosts;

namespace TAOM.Adapters;

/// <summary>
/// Builds the <see cref="PartyIntelSnapshot"/> for Hidden Hosts (#846). Read for every visible party nameplate every map
/// frame, so the costly reads are cached on real time: the main party's seeing range and Scouting skill, and each
/// party's spotting ratio (a terrain lookup in the engine's visibility model, called through
/// <c>Campaign.Models.MapVisibilityModel</c> so TAOM's override applies). Neither value changes inside half a second in a
/// way the player could see, and a value cached by an earlier campaign (a party's StringId can repeat after a load)
/// self-heals within that bound. The cache holds numbers only, never a campaign object: a faction or hero kept here
/// would outlive its campaign. Main thread only. Reads the saved fields it needs and writes none.
/// </summary>
public sealed class PartyIntelAdapter : IPartyIntelAdapter
{
    /// <summary>Spotting ratios are dropped past this many parties (destroyed ones are never removed).</summary>
    private const int MaxCachedParties = 4096;

    private static readonly long RefreshTicks = Stopwatch.Frequency / 2;

    private readonly Dictionary<string, PartyEntry> _spotting = new();
    private long _mainStamp;
    private bool _hasMain;
    private float _mainSeeingRange;
    private int _mainSkill;

    /// <summary>What is cached per party for half a second: the spotting ratio and whether the party is at war with the main party. Numbers only.</summary>
    private struct PartyEntry
    {
        internal float Ratio;
        internal bool AtWar;
        internal long Stamp;
    }

    public PartyIntelSnapshot Read(MobileParty? party)
    {
        var campaign = Campaign.Current;
        var main = campaign?.MainParty;
        var id = party?.StringId;
        if (campaign == null || main == null || party == null || id == null || party.Party == null)
            return PartyIntelSnapshot.NoData;

        long now = Stopwatch.GetTimestamp();
        RefreshMain(main, now);
        var mainFaction = main.MapFaction;
        var entry = Entry(campaign, party, id, now, mainFaction);

        return new PartyIntelSnapshot
        {
            HasData = true,
            StringId = id,
            HealthyCount = SandBoxUIHelper.GetPartyHealthyCount(party),
            OwnHealthyCount = party.Party.NumberOfHealthyMembers,
            DistanceToMain = party.Position.Distance(main.Position),
            MainSeeingRange = _mainSeeingRange,
            SpottingRatio = entry.Ratio,
            AtWar = entry.AtWar,
            NowHour = NowHour(),
            LeadsArmy = party.Army != null && party.Army.LeaderParty == party,
            ScoutingSkill = _mainSkill,
            IsVisible = party.IsVisible,
            InPlainSight = InPlainSight(campaign, main, party),
            IsMainParty = party == main,
            InPlayerClan = party.ActualClan != null && party.ActualClan == Clan.PlayerClan,
            InPlayerArmy = main.Army != null && party.Army == main.Army,
            TrueSight = campaign.TrueSight,
            SameFaction = mainFaction != null && party.MapFaction == mainFaction,
            ArmyLeaderSameFaction = mainFaction != null && party.Army?.LeaderParty?.MapFaction == mainFaction,
            IsInfoHidden = party.IsInfoHidden,
        };
    }

    public SettlementIntelSnapshot ReadSettlement(Settlement? settlement)
    {
        var campaign = Campaign.Current;
        var main = campaign?.MainParty;
        var id = settlement?.StringId;
        if (campaign == null || main == null || settlement == null || id == null)
            return SettlementIntelSnapshot.NoData;

        RefreshMain(main, Stopwatch.GetTimestamp());
        var mainFaction = main.MapFaction;
        return new SettlementIntelSnapshot
        {
            HasData = true,
            StringId = id,
            DefenderCount = SettlementDefenders.Count(settlement),
            NowHour = NowHour(),
            DistanceToMain = settlement.GatePosition.Distance(main.Position),
            MainSeeingRange = _mainSeeingRange,
            ScoutingSkill = _mainSkill,
            IsPlayerSettlement = main.CurrentSettlement == settlement,
            OwnedByPlayerClan = settlement.OwnerClan != null && settlement.OwnerClan == Clan.PlayerClan,
            PlayerClanHeroInside = HeroOfPlayerClanInside(settlement),
            SameFaction = mainFaction != null && settlement.MapFaction == mainFaction,
            TrueSight = campaign.TrueSight,
        };
    }

    public EventIntelSnapshot ReadMapEvent(MapEvent? mapEvent)
    {
        var campaign = Campaign.Current;
        var main = campaign?.MainParty;
        if (campaign == null || main == null || mapEvent == null)
            return EventIntelSnapshot.NoData;

        RefreshMain(main, Stopwatch.GetTimestamp());
        var mainFaction = main.MapFaction;
        int total = HealthyOfParties(mapEvent.InvolvedParties, skipHidden: true);
        return new EventIntelSnapshot
        {
            HasData = true,
            TotalHealthy = total,
            DistanceToMain = mapEvent.Position.Distance(main.Position),
            MainSeeingRange = _mainSeeingRange,
            ScoutingSkill = _mainSkill,
            PlayerInvolved = mapEvent.IsPlayerMapEvent || PlayerAmong(mapEvent.InvolvedParties, main),
            SameFaction = mainFaction != null
                          && (mapEvent.AttackerSide?.LeaderParty?.MapFaction == mainFaction
                              || mapEvent.DefenderSide?.LeaderParty?.MapFaction == mainFaction),
            TrueSight = campaign.TrueSight,
        };
    }

    public EventIntelSnapshot ReadSiegeEvent(SiegeEvent? siegeEvent)
    {
        var campaign = Campaign.Current;
        var main = campaign?.MainParty;
        var besieged = siegeEvent?.BesiegedSettlement;
        var camp = siegeEvent?.BesiegerCamp;
        if (campaign == null || main == null || siegeEvent == null || besieged == null || camp == null)
            return EventIntelSnapshot.NoData;

        RefreshMain(main, Stopwatch.GetTimestamp());
        var mainFaction = main.MapFaction;
        var besiegers = camp.GetInvolvedPartiesForEventType();
        var defenders = besieged.GetInvolvedPartiesForEventType();
        int total = HealthyOfParties(besiegers, skipHidden: false) + HealthyOfParties(defenders, skipHidden: false);
        return new EventIntelSnapshot
        {
            HasData = true,
            TotalHealthy = total,
            DistanceToMain = besieged.GatePosition.Distance(main.Position),
            MainSeeingRange = _mainSeeingRange,
            ScoutingSkill = _mainSkill,
            PlayerInvolved = main.SiegeEvent == siegeEvent || main.CurrentSettlement == besieged
                             || PlayerAmong(besiegers, main) || PlayerAmong(defenders, main),
            SameFaction = mainFaction != null && (camp.MapFaction == mainFaction || besieged.MapFaction == mainFaction),
            TrueSight = campaign.TrueSight,
        };
    }

    private static bool HeroOfPlayerClanInside(Settlement settlement)
    {
        var heroes = Clan.PlayerClan?.Heroes;
        if (heroes == null) return false;
        for (int i = 0; i < heroes.Count; i++)
            if (heroes[i].CurrentSettlement == settlement) return true;
        return false;
    }

    /// <summary>
    /// The healthy troops the vanilla tooltips total: every involved party that is not a settlement. The battle tooltip
    /// (<c>MapEventParty</c> overload) leaves an <c>IsInfoHidden</c> party out and prints "+ ?" instead, so the battle read passes
    /// <paramref name="skipHidden"/>; the siege tooltip (<c>PartyBase</c> list overload) has no such branch and counts every
    /// party, so the siege read does not. Either way the verdict never has more digits than any number the tooltip prints.
    /// </summary>
    private static int HealthyOfParties(IEnumerable<PartyBase> parties, bool skipHidden)
    {
        int sum = 0;
        foreach (var party in parties)
        {
            if (party.IsSettlement) continue;
            if (skipHidden && party.MobileParty != null && party.MobileParty.IsInfoHidden) continue;
            sum += party.NumberOfHealthyMembers;
        }
        return sum;
    }

    private static bool PlayerAmong(IEnumerable<PartyBase> parties, MobileParty main)
    {
        foreach (var party in parties)
        {
            var mobile = party.MobileParty;
            if (mobile == null) continue;
            if (mobile == main || (mobile.ActualClan != null && mobile.ActualClan == Clan.PlayerClan)
                || (main.Army != null && mobile.Army == main.Army))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A party the player is looking straight at: inside the main party's current settlement, the one it is encountering or
    /// talking to, or in its map event or its own siege (either side). Its numbers are exact by distance already, and the settlement and
    /// encounter menus that list such parties must not blur.
    /// </summary>
    private static bool InPlainSight(Campaign campaign, MobileParty main, MobileParty party)
    {
        var settlement = party.CurrentSettlement;
        if (settlement != null && settlement == main.CurrentSettlement) return true;

        var encountered = PlayerEncounter.EncounteredParty;
        if (encountered != null && encountered == party.Party) return true;

        var mapEvent = party.MapEvent;
        if (mapEvent != null && mapEvent == main.MapEvent) return true;

        // The player's siege on either side: MobileParty.SiegeEvent is the besieger camp's event and null for a defender, so the
        // engine's own PlayerSiege.PlayerSiegeEvent (the main party's, else the one of the settlement it is in) is the siege,
        // and a party is in it when it is in the camp or inside the besieged settlement.
        var playerSiege = PlayerSiege.PlayerSiegeEvent;
        if (playerSiege != null
            && (party.SiegeEvent == playerSiege
                || (party.CurrentSettlement != null && party.CurrentSettlement == playerSiege.BesiegedSettlement)))
            return true;

        var talking = campaign.ConversationManager?.ConversationParty;
        return talking != null && talking == party;
    }

    private void RefreshMain(MobileParty main, long now)
    {
        if (_hasMain && now - _mainStamp < RefreshTicks) return;

        _mainStamp = now;
        _mainSeeingRange = main.SeeingRange;
        _mainSkill = main.EffectiveScout?.GetSkillValue(DefaultSkills.Scouting) ?? 0;
        _hasMain = true;
    }

    public SettlementIntelSnapshot CollectPlainSight(List<PartyIntelSnapshot> into)
    {
        into.Clear();
        var campaign = Campaign.Current;
        var main = campaign?.MainParty;
        if (campaign == null || main == null) return SettlementIntelSnapshot.NoData;

        RefreshMain(main, Stopwatch.GetTimestamp());
        int hour = NowHour();
        var inside = main.CurrentSettlement;
        if (inside != null)
            for (int i = 0; i < inside.Parties.Count; i++) AddLight(into, campaign, main, inside.Parties[i], hour);

        AddLight(into, campaign, main, PlayerEncounter.EncounteredParty?.MobileParty, hour);
        AddLight(into, campaign, main, campaign.ConversationManager?.ConversationParty, hour);
        var mapEvent = main.MapEvent;
        if (mapEvent != null)
            foreach (var party in mapEvent.InvolvedParties) AddLight(into, campaign, main, party.MobileParty, hour);

        var siege = PlayerSiege.PlayerSiegeEvent;
        if (siege != null)
        {
            if (siege.BesiegerCamp != null)
                foreach (var party in siege.BesiegerCamp.GetInvolvedPartiesForEventType()) AddLight(into, campaign, main, party.MobileParty, hour);
            if (siege.BesiegedSettlement != null)
                foreach (var party in siege.BesiegedSettlement.GetInvolvedPartiesForEventType()) AddLight(into, campaign, main, party.MobileParty, hour);
        }
        return inside == null ? SettlementIntelSnapshot.NoData : ReadSettlement(inside);
    }

    /// <summary>
    /// A candidate from the lists above is kept only when <see cref="InPlainSight"/> says so, so "in plain sight" has one
    /// definition. The snapshot is light: no spotting-ratio terrain query and no war check for every occupant (those always
    /// miss the half-second cache), only what the exact stamp reads.
    /// </summary>
    private void AddLight(List<PartyIntelSnapshot> into, Campaign campaign, MobileParty main, MobileParty? party, int hour)
    {
        var id = party?.StringId;
        if (party == null || id == null || party.Party == null || !InPlainSight(campaign, main, party)) return;

        into.Add(new PartyIntelSnapshot
        {
            HasData = true,
            StringId = id,
            HealthyCount = SandBoxUIHelper.GetPartyHealthyCount(party),
            OwnHealthyCount = party.Party.NumberOfHealthyMembers,
            ScoutingSkill = _mainSkill,
            NowHour = hour,
            IsVisible = party.IsVisible,
            InPlainSight = true,
            IsMainParty = party == main,
            InPlayerClan = party.ActualClan != null && party.ActualClan == Clan.PlayerClan,
            InPlayerArmy = main.Army != null && party.Army == main.Army,
            TrueSight = campaign.TrueSight,
            IsInfoHidden = party.IsInfoHidden,
        });
    }

    /// <summary>The campaign clock in whole hours, read live: it is one property read and the memory's stamps must not lag an hour.</summary>
    private static int NowHour() => HiddenHostsMath.ToWholeHours(CampaignTime.Now.ToHours);

    private PartyEntry Entry(Campaign campaign, MobileParty party, string id, long now, IFaction? mainFaction)
    {
        if (_spotting.TryGetValue(id, out var entry) && now - entry.Stamp < RefreshTicks)
            return entry;

        if (_spotting.Count >= MaxCachedParties) _spotting.Clear();
        entry = new PartyEntry
        {
            Ratio = campaign.Models.MapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange(party),
            AtWar = mainFaction != null && party.MapFaction != null && FactionManager.IsAtWarAgainstFaction(party.MapFaction, mainFaction),
            Stamp = now,
        };
        _spotting[id] = entry;
        return entry;
    }
}
