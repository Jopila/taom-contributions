using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IFortHoldAdapter"/> over the live campaign (v1.5.5, #871). A leader is found by its party id on every call and
/// nothing is kept, so no engine object outlives a save load. The only writes are the two calls the engine itself makes
/// on a capturing army leader: <c>MobilePartyAi.DisableForHours</c> (<c>KingdomManager.SiegeCompleted</c>, 10 hours) and
/// <c>EnableAi</c> (<c>MobilePartyAi.TickInternal</c> when a pause ends). The side of a realm is the id and the culture of
/// that same realm, built by <see cref="FactionSides"/>.
/// </summary>
public sealed class FortHoldAdapter : IFortHoldAdapter
{
    public double CampaignHours => Campaign.Current == null ? double.NaN : CampaignTime.Now.ToHours;

    public IReadOnlyList<FortArmyReading> ReadArmiesIn(string fortId)
    {
        var readings = new List<FortArmyReading>();
        var fort = FindFort(fortId);
        var parties = fort?.Parties;
        if (fort == null || parties == null)
            return readings;

        for (int i = 0; i < parties.Count; i++)
        {
            var party = parties[i];
            if (party != null && party.IsActive && party.Army != null && party.Army.LeaderParty == party)
                readings.Add(Read(party, fort));
        }
        return readings;
    }

    public FortArmyReading ReadArmy(string leaderPartyId, string fortId)
    {
        var party = FindParty(leaderPartyId);
        var fort = FindFort(fortId);
        if (party == null || fort == null || !party.IsActive || party.Army == null || party.Army.LeaderParty != party)
            return FortArmyReading.Missing;
        return Read(party, fort);
    }

    public void HoldForHours(string leaderPartyId, int hours)
    {
        var party = FindParty(leaderPartyId);
        if (party != null && party.IsActive && hours > 0)
            party.Ai.DisableForHours(hours);
    }

    public void ResumeAi(string leaderPartyId)
    {
        var party = FindParty(leaderPartyId);
        if (party != null && party.IsActive)
            party.Ai.EnableAi();
    }

    private static MobileParty? FindParty(string id) => Campaign.Current?.CampaignObjectManager.Find<MobileParty>(id);

    private static Settlement? FindFort(string id)
    {
        var settlement = Settlement.Find(id);
        return settlement != null && (settlement.IsTown || settlement.IsCastle) ? settlement : null;
    }

    private static FortArmyReading Read(MobileParty leader, Settlement fort)
    {
        var army = leader.Army;
        var hero = leader.LeaderHero;
        var main = MobileParty.MainParty;
        return new FortArmyReading
        {
            Exists = true,
            LeaderPartyId = leader.StringId,
            InFort = leader.CurrentSettlement == fort,
            AiPaused = leader.Ai.IsDisabled,
            SameRealmAsFort = leader.MapFaction != null && leader.MapFaction == fort.MapFaction,
            // The leader entering the fortress is vanilla's call; an army the player joined is the player's to move.
            IsPlayerLed = leader.IsMainParty || (main != null && main.Army != null && main.Army == army),
            InMapEvent = leader.MapEvent != null,
            FortUnderSiege = fort.IsUnderSiege,
            Cohesion = army.Cohesion,
            LowestFoodDays = LowestFoodDays(leader),
            Valor = hero?.GetTraitLevel(DefaultTraits.Valor) ?? 0,
            Calculating = hero?.GetTraitLevel(DefaultTraits.Calculating) ?? 0,
            Side = FactionSides.Of(leader.MapFaction),
        };
    }

    /// <summary>
    /// The leanest party of the army, the leader included. <c>MobileParty.GetNumDaysForFoodToLast</c> counts a day as
    /// <c>TotalFood / -FoodChange</c> but casts the quotient to int, so a party that eats nothing (a division by zero) reads
    /// as int.MinValue, which a "less than a day" test would take for starvation. The same quotient is taken here as a
    /// float: positive infinity when no party eats, NaN when a reading is not a number.
    /// </summary>
    private static float LowestFoodDays(MobileParty leader)
    {
        float lowest = FoodDays(leader);
        var attached = leader.AttachedParties;
        for (int i = 0; i < attached.Count; i++)
        {
            float days = FoodDays(attached[i]);
            if (float.IsNaN(days))
                return float.NaN;
            if (days < lowest)
                lowest = days;
        }
        return lowest;
    }

    private static float FoodDays(MobileParty party)
    {
        float change = party.FoodChange;
        if (float.IsNaN(change))
            return float.NaN;
        if (change >= 0f)
            return float.PositiveInfinity;
        return party.ItemRoster.TotalFood / -change;
    }
}
