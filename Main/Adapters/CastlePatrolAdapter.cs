using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICastlePatrolAdapter"/>. The Guard House level is read straight from
/// <c>Town.Buildings</c> (a short list, found by building type), never through <see cref="TownBuildingsAdapter.GetLevel"/>,
/// which does a settlement lookup on every call. Anything that is not a castle returns before a building is touched.
/// </summary>
public sealed class CastlePatrolAdapter : ICastlePatrolAdapter
{
    public CastlePatrolSnapshot Read(Settlement? settlement)
    {
        if (settlement == null || !settlement.IsCastle)
            return default;

        // Each flag is "this strength has a template": the same mapping TemplateFor serves, so it is false without an owner clan.
        return new CastlePatrolSnapshot(
            IsCastle: true,
            OwnerIsRebel: settlement.OwnerClan is { IsRebelClan: true },
            GuardHouseLevel: GuardHouseLevel(settlement),
            HasWeakTemplate: TemplateFor(settlement, 1) != null,
            HasModerateTemplate: TemplateFor(settlement, 2) != null,
            HasStrongTemplate: TemplateFor(settlement, 3) != null,
            PatrolAlive: settlement.PatrolParty != null);
    }

    public PartyTemplateObject? TemplateFor(Settlement settlement, int strength)
    {
        var culture = settlement.OwnerClan?.Culture;
        return strength == 1 ? culture?.SettlementPatrolPartyTemplateWeak
            : strength == 2 ? culture?.SettlementPatrolPartyTemplateModerate
            : strength == 3 ? culture?.SettlementPatrolPartyTemplateStrong
            : null;
    }

    public int CastlePatrolGuardHouseLevel(MobileParty? party)
    {
        if (party == null || !party.IsPatrolParty)
            return CastlePatrolSnapshot.NotACastlePatrol;

        var home = party.HomeSettlement;
        return home != null && home.IsCastle ? GuardHouseLevel(home) : CastlePatrolSnapshot.NotACastlePatrol;
    }

    private static int GuardHouseLevel(Settlement castle)
    {
        var buildings = castle.Town?.Buildings;
        if (buildings == null || Campaign.Current == null)
            return 0;

        var guardHouse = DefaultBuildingTypes.CastleGuardHouse;
        foreach (var building in buildings)
        {
            if (building.BuildingType == guardHouse)
                return building.CurrentLevel;
        }

        return 0;
    }
}
