using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.RedArrow;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IPlayerSiegeStateAdapter"/> over the live campaign (v1.5.5, #873). Without a campaign
/// (<c>Campaign.Current?.MainParty</c> is null) both reads return the all-false state; past that guard the player's hero is
/// read as <c>Hero.MainHero</c>, as vanilla reads it. The map-action facts mirror
/// <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c> (:2440-2497): the raid fact is vanilla's village-being-raided and
/// main-party-raid-map-event pair, and the own-siege fact compares the encounter's settlement with the settlement the
/// player's besieger camp besieges.
/// </summary>
public sealed class PlayerSiegeStateAdapter : IPlayerSiegeStateAdapter
{
    public PlayerSiegeState ReadSiege()
    {
        var main = Campaign.Current?.MainParty;
        if (main == null)
            return default;

        return new PlayerSiegeState(isBesieger: main.BesiegerCamp != null, leadsArmy: main.Army?.LeaderParty == main);
    }

    public MapActionState ReadMapAction()
    {
        var main = Campaign.Current?.MainParty;
        if (main == null)
            return default;

        var hero = Hero.MainHero;

        var hasEncounter = PlayerEncounter.Current != null;
        var settlement = hasEncounter ? PlayerEncounter.EncounterSettlement : null;
        var mapEvent = main.MapEvent;
        var raid = settlement?.Village != null
                   && settlement.Village.VillageState == Village.VillageStates.BeingRaided
                   && mapEvent != null
                   && mapEvent.IsRaid;
        return new MapActionState(
            isPrisoner: hero.IsPrisoner,
            isInRaftState: main.IsInRaftState,
            isInFerryState: main.IsInFerryState,
            inMission: CampaignMission.Current != null,
            hasEncounter: hasEncounter,
            encounterHasSettlement: settlement != null,
            raidAtEncounter: raid,
            encounterSettlementUnderSiege: settlement != null && settlement.IsUnderSiege,
            encounterSettlementIsOwnSiege: settlement != null && settlement == main.BesiegedSettlement,
            hasMapEvent: mapEvent != null);
    }
}
