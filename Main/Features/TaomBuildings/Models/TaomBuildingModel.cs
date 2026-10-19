using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace TAOM.Features.TaomBuildings.Models;

/// <summary>
/// Decides which TAOM building type a new-game town receives. The engine's new-game pass offers every registered
/// building type to every fortification and asks this model first (<c>BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart</c>,
/// the only caller of <c>CanAddBuildingTypeToTown</c>): a TAOM type is allowed only when it is the variant of the town's
/// culture, any other type is the base model's answer. Existing saves are seeded by <c>TaomBuildingSeedingBehavior</c>.
/// </summary>
public sealed class TaomBuildingModel : DefaultBuildingModel
{
    private readonly TaomBuildingCatalog _catalog;

    public TaomBuildingModel(TaomBuildingCatalog catalog)
    {
        _catalog = catalog;
    }

    public override bool CanAddBuildingTypeToTown(BuildingType buildingType, Town town) =>
        _catalog.CanAdd(buildingType.StringId, town.Settlement?.Culture?.StringId) ?? base.CanAddBuildingTypeToTown(buildingType, town);
}
