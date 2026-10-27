using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Features.CreatureBandits;

namespace TAOM.Adapters;

/// <summary>
/// The raiding hosts' engine boundary (#870; members verified against the v1.5.5 decompile: BanditPartyComponent.cs,
/// MobileParty.cs, MobilePartyAi.cs, SetPartyAiAction.cs, DestroyPartyAction.cs, Village.cs, Settlement.cs,
/// NavigationHelper.cs, Army.cs, SettlementHelper.cs). Every call looks its party up by StringId in the live campaign and
/// the adapter keeps no state, no <c>Campaign</c> and no <c>Village</c> (a village reaches the whole campaign graph), so a
/// loaded save never meets an object from the last one. It reads and acts; every decision is the service's. Main thread
/// only: the campaign's daily, hourly and party events.
/// </summary>
public sealed class RaidHostAdapter : IRaidHostAdapter
{
    private static MobileParty? FindParty(string id) => Campaign.Current?.CampaignObjectManager.Find<MobileParty>(id);

    private static Clan? FindClan(string id) => Clan.All.FirstOrDefault(c => c.StringId == id);

    // ---- creating a host ---------------------------------------------------------------------------------------------

    public RaidSpawnFailure Probe(string clanId, string anchorId)
    {
        var clan = FindClan(clanId);
        if (clan == null)
            return RaidSpawnFailure.ClanMissing;
        var anchor = Settlement.Find(anchorId);
        if (anchor == null)
            return RaidSpawnFailure.AnchorMissing;
        return NearestHideout(clan, anchor) == null ? RaidSpawnFailure.NoHideout : RaidSpawnFailure.None;
    }

    public bool TryGetAnchorOwner(string anchorId, out string factionId, out string cultureId, out bool isKingdom)
    {
        var owner = Settlement.Find(anchorId)?.MapFaction;
        factionId = owner?.StringId ?? string.Empty;
        cultureId = owner?.Culture?.StringId ?? string.Empty;
        isKingdom = owner?.IsKingdomFaction == true;
        return owner != null;
    }

    /// <summary>The hideout of the clan's culture nearest to the anchor. A bandit party must have one: a null home breaks vanilla's per-hideout count.</summary>
    private static Hideout? NearestHideout(Clan clan, Settlement anchor)
    {
        Hideout? best = null;
        float bestDistance = float.MaxValue;
        foreach (var hideout in Hideout.All)
        {
            var settlement = hideout?.Settlement;
            if (settlement == null || settlement.Culture != clan.Culture)
                continue;
            float distance = settlement.Position.DistanceSquared(anchor.Position);
            if (distance < bestDistance)
            {
                best = hideout;
                bestDistance = distance;
            }
        }
        return best;
    }

    public RaidHostSpawn SpawnHost(string clanId, string anchorId, string keyedName, float spawnRadiusDays)
    {
        var clan = FindClan(clanId);
        if (clan == null)
            return Fail(RaidSpawnFailure.ClanMissing);
        var anchor = Settlement.Find(anchorId);
        if (anchor == null)
            return Fail(RaidSpawnFailure.AnchorMissing);
        var hideout = NearestHideout(clan, anchor);
        if (hideout == null)
            return Fail(RaidSpawnFailure.NoHideout);

        // The engine's point within the radius of the anchor's gate, or the gate itself when it finds none (250 tries);
        // then vanilla's out-of-sight retry, which keeps this first point when all 15 retries fail.
        float radius = spawnRadiusDays * Campaign.Current.EstimatedAverageBanditPartySpeed * CampaignTime.HoursInDay;
        var point = NavigationHelper.FindPointAroundPosition(anchor.GatePosition, MobileParty.NavigationType.Default, radius);
        point = CreatureBandParties.OutOfPlayerSight(point, radius);
        if (!point.IsValid())
            return Fail(RaidSpawnFailure.NoPoint);

        // No template: the roster is the service's. A null home would crash BanditSpawnCampaignBehavior (its per-hideout
        // count keys on HomeSettlement), so the hideout is the clan's nearest, and SetCustomHomeSettlement is never used.
        var party = BanditPartyComponent.CreateBanditParty(clan.StringId + "_fat", clan, hideout, false, null, point);
        if (party == null)
            return Fail(RaidSpawnFailure.CreateFailed);
        party.Party.SetCustomName(new TextObject(keyedName));
        string? nearestTown = SettlementHelper.FindNearestSettlementToPoint(point, s => s.IsTown)?.StringId;
        return new RaidHostSpawn(RaidSpawnFailure.None, party.StringId, hideout.Settlement.StringId, nearestTown);
    }

    private static RaidHostSpawn Fail(RaidSpawnFailure failure) => new RaidHostSpawn(failure, null, null, null);

    public float TroopPower(string troopId)
    {
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        var model = Campaign.Current?.Models?.MilitaryPowerModel;
        if (troop == null || model == null)
            return float.NaN;
        return model.GetTroopPower(troop, BattleSideEnum.Defender, MapEvent.PowerCalculationContext.Estimated, 0f);
    }

    public int AddTroops(string partyId, string troopId, int count)
    {
        var party = FindParty(partyId);
        var troop = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        if (party?.MemberRoster == null || troop == null || count <= 0)
            return 0;
        party.MemberRoster.AddToCounts(troop, count);
        return count;
    }

    public void CompleteSpawn(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Ai == null)
            return;
        // Vanilla's own steps for a looter party (BanditSpawnCampaignBehavior.InitializeBanditParty): visual dirty after
        // the roster exists, then the trade purse. A bandit party never eats and has no wages. The pin is the last step.
        party.Party.SetVisualAsDirty();
        party.InitializePartyTrade(0);
        party.Ai.SetDoNotMakeNewDecisions(true);
    }

    public void ReleasePin(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Ai == null)
            return;
        party.Ai.SetDoNotMakeNewDecisions(false);
        // A pinned party returns before its think counter advances, so the last order could stand for hours otherwise.
        party.Ai.RethinkAtNextHourlyTick = true;
    }

    // ---- reading -----------------------------------------------------------------------------------------------------

    public RaidHostSnapshot Read(string partyId)
    {
        var party = FindParty(partyId);
        if (party?.Party == null)
            return RaidHostSnapshot.Missing;

        var at = party.Position.ToVec2();
        var home = party.HomeSettlement;
        return new RaidHostSnapshot
        {
            Exists = true,
            IsActive = party.IsActive,
            InMapEvent = party.MapEvent != null,
            Strength = party.Party.EstimatedStrength,
            // Speed asserts on an inactive party (MobileParty.cs:419-431).
            Speed = party.IsActive ? party.Speed : 0f,
            X = at.x,
            Y = at.y,
            HasHome = home != null,
            DistanceToHome = home != null ? party.Position.Distance(home.Position) : float.NaN,
            RaidTargetId = party.DefaultBehavior == AiBehavior.RaidSettlement ? party.TargetSettlement?.StringId : null,
        };
    }

    // ---- villages ----------------------------------------------------------------------------------------------------

    private static string? KingdomIdOf(Village village) =>
        village.Bound != null && village.MapFaction is Kingdom kingdom ? kingdom.StringId : null;

    private static VillageReading ReadingOf(Village village)
    {
        var settlement = village.Settlement;
        var at = settlement.Position.ToVec2();
        // The owner is the bound fief's clan (Settlement.OwnerClan reads Village.Bound.OwnerClan, which would throw with no bound fief).
        return new VillageReading(true, KingdomIdOf(village), village.VillageState == Village.VillageStates.Normal,
            village.Hearth, settlement.StringId, at.x, at.y, village.Bound?.OwnerClan?.StringId);
    }

    public IReadOnlyList<VillageReading> ReadVillages()
    {
        // Live on every call, never kept: a village reaches its settlement and the campaign. The caller asks for the
        // list only when it picks a target or a village (a daily roll, a new raid order), never every hour per host.
        var readings = new List<VillageReading>();
        foreach (var village in Village.All)
        {
            if (village?.Settlement != null)
                readings.Add(ReadingOf(village));
        }
        return readings;
    }

    public VillageReading ReadVillage(string villageId)
    {
        var village = Settlement.Find(villageId)?.Village;
        return village?.Settlement == null ? VillageReading.Missing : ReadingOf(village);
    }

    // ---- parties near ------------------------------------------------------------------------------------------------

    public IReadOnlyList<NearbyParty> PartiesNear(string hostPartyId, float radius)
    {
        var found = new List<NearbyParty>();
        var host = FindParty(hostPartyId);
        if (host == null || !(radius > 0f) || float.IsInfinity(radius))
            return found;

        // The locator grid around the host, the search vanilla's own visibility pass runs. It skips a party at d^2 >= r^2,
        // so the service keeps the exact filter on the distance it reads here.
        var search = MobileParty.StartFindingLocatablesAroundPosition(host.Position.ToVec2(), radius * 1.01f);
        for (var p = MobileParty.FindNextLocatable(ref search); p != null; p = MobileParty.FindNextLocatable(ref search))
        {
            if (p == host || !p.IsActive || p.Party == null || !(p.MapFaction is Kingdom kingdom))
                continue;
            // What a host must not engage: PartyBase.IInteractablePoint.CanPartyInteract refuses an ignored party, and
            // vanilla weighs a besieger by its whole camp (DefaultMobilePartyAIModel, near line 208), so any party in a
            // siege camp, leader or not, is left alone.
            if (p.ShouldBeIgnored || p.BesiegerCamp != null)
                continue;
            // Vanilla weighs an army's leader by its whole army (DefaultMobilePartyAIModel, near line 208): a battle
            // with the leader pulls the army in. A member attached to a leader is the caller's IsAttached filter.
            var army = p.Army;
            float strength = army != null && army.LeaderParty == p ? army.EstimatedStrength : p.Party.EstimatedStrength;
            found.Add(new NearbyParty
            {
                Id = p.StringId,
                KingdomId = kingdom.StringId,
                InMapEvent = p.MapEvent != null,
                InSettlement = p.CurrentSettlement != null,
                IsAttached = p.AttachedTo != null,
                IsMainParty = p.IsMainParty,
                LeadsMainParty = army != null && army.LeaderParty == p && MobileParty.MainParty?.AttachedTo == p,
                Strength = strength,
                Speed = p.Speed,
                Distance = p.Position.Distance(host.Position),
            });
        }
        return found;
    }

    // ---- orders ------------------------------------------------------------------------------------------------------

    public void SpareMainParty(string hostPartyId, int hours)
    {
        var host = FindParty(hostPartyId);
        if (host?.Ai == null || !host.IsActive || hours <= 0)
            return;
        host.Ai.SetDoNotAttackMainParty(hours);
    }

    public void OrderEngage(string hostPartyId, string targetPartyId)
    {
        var host = FindParty(hostPartyId);
        var target = FindParty(targetPartyId);
        if (host?.Ai == null || !host.IsActive || target == null || !target.IsActive)
            return;
        // Only the raid and go-home orders are no-ops while the same order stands. The engage guard compares the owner
        // with the target party (SetPartyAiAction.ApplyInternal), which never matches, so it re-applies each hour: harmless.
        SetPartyAiAction.GetActionForEngagingParty(host, target, MobileParty.NavigationType.Default, false);
        host.Ai.CheckPartyNeedsUpdate();
    }

    public void OrderRaid(string hostPartyId, string villageId)
    {
        var host = FindParty(hostPartyId);
        var village = Settlement.Find(villageId);
        if (host?.Ai == null || !host.IsActive || village?.Village == null)
            return;
        // A no-op while the same raid order stands.
        SetPartyAiAction.GetActionForRaidingSettlement(host, village, MobileParty.NavigationType.Default, false, false);
        host.Ai.CheckPartyNeedsUpdate();
    }

    public void OrderGoHome(string hostPartyId)
    {
        var host = FindParty(hostPartyId);
        var home = host?.HomeSettlement;
        if (host?.Ai == null || !host.IsActive || home == null)
            return;
        // A no-op while the same visit order stands.
        SetPartyAiAction.GetActionForVisitingSettlement(host, home, MobileParty.NavigationType.Default, false, false);
        host.Ai.CheckPartyNeedsUpdate();
    }

    public bool Despawn(string partyId)
    {
        var party = FindParty(partyId);
        // The main party is never ours to destroy; a party in a battle is finalized by the engine.
        if (party == null || party.IsMainParty || !party.IsActive || party.MapEvent != null)
            return false;
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);
        DestroyPartyAction.Apply(null, party);
        return true;
    }
}
