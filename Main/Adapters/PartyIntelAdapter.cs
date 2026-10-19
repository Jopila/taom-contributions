using System.Collections.Generic;
using System.Diagnostics;
using SandBox.ViewModelCollection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

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

    private readonly Dictionary<string, SpottingEntry> _spotting = new();
    private long _mainStamp;
    private bool _hasMain;
    private float _mainSeeingRange;
    private int _mainSkill;

    private struct SpottingEntry
    {
        internal float Ratio;
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

        return new PartyIntelSnapshot
        {
            HasData = true,
            StringId = id,
            HealthyCount = SandBoxUIHelper.GetPartyHealthyCount(party),
            DistanceToMain = party.Position.Distance(main.Position),
            MainSeeingRange = _mainSeeingRange,
            SpottingRatio = SpottingRatio(campaign, party, id, now),
            ScoutingSkill = _mainSkill,
            IsMainParty = party == main,
            InPlayerClan = party.ActualClan != null && party.ActualClan == Clan.PlayerClan,
            InPlayerArmy = main.Army != null && party.Army == main.Army,
            TrueSight = campaign.TrueSight,
            SameFaction = mainFaction != null && party.MapFaction == mainFaction,
            ArmyLeaderSameFaction = mainFaction != null && party.Army?.LeaderParty?.MapFaction == mainFaction,
            IsInfoHidden = party.IsInfoHidden,
        };
    }

    private void RefreshMain(MobileParty main, long now)
    {
        if (_hasMain && now - _mainStamp < RefreshTicks) return;

        _mainStamp = now;
        _mainSeeingRange = main.SeeingRange;
        _mainSkill = main.EffectiveScout?.GetSkillValue(DefaultSkills.Scouting) ?? 0;
        _hasMain = true;
    }

    private float SpottingRatio(Campaign campaign, MobileParty party, string id, long now)
    {
        if (_spotting.TryGetValue(id, out var entry) && now - entry.Stamp < RefreshTicks)
            return entry.Ratio;

        if (_spotting.Count >= MaxCachedParties) _spotting.Clear();
        float ratio = campaign.Models.MapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange(party);
        _spotting[id] = new SpottingEntry { Ratio = ratio, Stamp = now };
        return ratio;
    }
}
