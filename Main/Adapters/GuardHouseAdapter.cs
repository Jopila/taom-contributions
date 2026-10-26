using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGuardHouseAdapter"/>. The Guard House is found in <c>Town.Buildings</c> (a short list)
/// by building type: the castle one for a castle, the town one for a town. Eligibility is vanilla's own filter from
/// <c>DefaultBuildingScoreCalculationModel.GetNextBuilding</c>, so a Guard House the AI could not have picked is never forced.
/// </summary>
public sealed class GuardHouseAdapter : IGuardHouseAdapter
{
    public GuardHouseCandidate Read(Town town)
    {
        var guardHouse = GuardHouse(town);
        return guardHouse != null && IsEligible(town, guardHouse)
            ? new GuardHouseCandidate(true, guardHouse.CurrentLevel, MBRandom.RandomFloat)
            : default;
    }

    public Building? GuardHouse(Town town)
    {
        if (town?.Buildings == null || Campaign.Current == null)
            return null;

        var guardHouseType = town.Settlement.IsCastle ? DefaultBuildingTypes.CastleGuardHouse : DefaultBuildingTypes.SettlementGuardHouse;
        foreach (var building in town.Buildings)
        {
            if (building.BuildingType == guardHouseType)
                return building;
        }

        return null;
    }

    private static bool IsEligible(Town town, Building building) =>
        !building.BuildingType.IsDailyProject && building.CurrentLevel < 3 && !town.BuildingsInProgress.Contains(building);
}
