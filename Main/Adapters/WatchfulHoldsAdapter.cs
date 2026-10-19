using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.WatchfulHolds;
using TAOM.Features.WatchfulHolds.Models;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IWatchfulHoldsAdapter"/> over the live campaign (v1.5.5). A party handle is the <c>MobileParty</c>
/// itself, a fort is found by its settlement id. The service keeps the fort list for the campaign. The losses go
/// through <c>TroopRoster</c> and <c>ItemRoster</c> with the counts clamped first: only
/// <c>RemoveNumberOfNonHeroTroopsRandomly</c> asserts on a request above the healthy regulars (and then clamps itself),
/// while <c>WoundNumberOfNonHeroTroopsRandomly</c> silently wounds fewer; the clamp keeps the reported counts true.
/// Both pick only non-hero troops.
/// </summary>
public sealed class WatchfulHoldsAdapter : IWatchfulHoldsAdapter
{
    public double CampaignHours => CampaignTime.Now.ToHours;

    public bool TryReadParty(object engineParty, out WatchfulPartySnapshot snapshot)
    {
        if (engineParty is MobileParty party && party.IsActive)
            return Read(party, out snapshot);
        snapshot = default;
        return false;
    }

    private static bool Read(MobileParty party, out WatchfulPartySnapshot snapshot)
    {
        var position = party.Position.ToVec2();
        var army = party.Army;
        var model = Campaign.Current?.Models?.PartyImpairmentModel;
        snapshot = new WatchfulPartySnapshot
        {
            Handle = party,
            X = position.x,
            Y = position.y,
            IsMainParty = party.IsMainParty,
            IsLordParty = party.IsLordParty,
            IsMoving = party.IsMoving,
            InSettlement = party.CurrentSettlement != null,
            InMapEvent = party.MapEvent != null,
            InSiegeEvent = party.SiegeEvent != null,
            IsArmyFollower = army != null && army.LeaderParty != party,
            IsArmyLeader = army != null && army.LeaderParty == party,
            IsDisorganized = party.IsDisorganized,
            CanGetDisorganized = model != null && model.CanGetDisorganized(party.Party),
            MenCount = party.MemberRoster?.TotalManCount ?? 0,
        };
        return true;
    }

    public FortSite[] GetFortSites()
    {
        var sites = new List<FortSite>();
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || !(settlement.IsCastle || settlement.IsTown))
                continue;
            var position = settlement.GetPosition2D;
            sites.Add(new FortSite(settlement.StringId, position.x, position.y));
        }
        return sites.ToArray();
    }

    public bool TryReadFort(string fortId, out FortStatus status)
    {
        var settlement = Settlement.Find(fortId);
        if (settlement == null || !(settlement.IsCastle || settlement.IsTown))
        {
            status = default;
            return false;
        }

        // The owner's culture is the owning clan's, with the realm's as the fallback for a fief the engine reports
        // no clan for. The garrison is every man in the garrison party, wounded included.
        status = new FortStatus
        {
            OwnerCultureId = settlement.OwnerClan?.Culture?.StringId ?? settlement.MapFaction?.Culture?.StringId ?? string.Empty,
            UnderSiege = settlement.IsUnderSiege,
            GarrisonMen = settlement.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0,
        };
        return true;
    }

    public bool AreAtWar(object partyHandle, string fortId)
    {
        var party = partyHandle as MobileParty;
        var settlement = Settlement.Find(fortId);
        return party != null && settlement != null
            && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction);
    }

    public IReadOnlyList<WatchfulPartySnapshot> GetArmyMemberParties(object leaderHandle)
    {
        var members = new List<WatchfulPartySnapshot>();
        if (!(leaderHandle is MobileParty leader) || !leader.IsActive)
            return members;

        if (Read(leader, out var leaderSnapshot))
            members.Add(leaderSnapshot);
        var attached = leader.AttachedParties;
        for (int i = 0; i < attached.Count; i++)
        {
            var member = attached[i];
            if (member != null && member.IsActive && Read(member, out var snapshot))
                members.Add(snapshot);
        }
        return members;
    }

    public void Disorganize(object handle) => (handle as MobileParty)?.SetDisorganized(true);

    public (int killed, int wounded) ApplyLosses(object handle, int kill, int wound)
    {
        var roster = (handle as MobileParty)?.MemberRoster;
        if (roster == null)
            return (0, 0);

        // Kill first, then wound, re-reading the healthy regulars between: the kills are no longer there to wound.
        int killed = WatchfulLossMath.ClampToHealthy(kill, HealthyRegulars(roster));
        if (killed > 0)
            roster.RemoveNumberOfNonHeroTroopsRandomly(killed);
        int wounded = WatchfulLossMath.ClampToHealthy(wound, HealthyRegulars(roster));
        if (wounded > 0)
            roster.WoundNumberOfNonHeroTroopsRandomly(wounded);
        return (killed, wounded);
    }

    public int RemoveFood(object handle, float fraction)
    {
        var roster = (handle as MobileParty)?.ItemRoster;
        if (roster == null)
            return 0;

        // Collect the stacks first: removing an emptied stack moves the last element into its slot.
        var elements = new List<EquipmentElement>();
        var amounts = new List<int>();
        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster[i];
            if (element.EquipmentElement.Item?.IsFood == true && element.Amount > 0)
            {
                elements.Add(element.EquipmentElement);
                amounts.Add(element.Amount);
            }
        }

        var plan = WatchfulLossMath.PlanFoodLoss(amounts.ToArray(), fraction, MBRandom.RandomInt);
        int removed = 0;
        for (int i = 0; i < plan.Length; i++)
        {
            if (plan[i] <= 0)
                continue;
            roster.AddToCounts(elements[i], -plan[i]);
            removed += plan[i];
        }
        return removed;
    }

    public void LowerMorale(object handle, float amount)
    {
        // A positive requirement: a NaN or non-positive amount changes nothing.
        if (handle is MobileParty party && amount > 0f)
            party.RecentEventsMorale -= amount;
    }

    public void LowerArmyCohesion(object leaderHandle, float amount, float floor)
    {
        var army = (leaderHandle as MobileParty)?.Army;
        if (army != null)
            army.Cohesion = WatchfulLossMath.CohesionAfterHit(army.Cohesion, amount, floor);
    }

    private static int HealthyRegulars(TroopRoster roster) => roster.TotalRegulars - roster.TotalWoundedRegulars;
}
