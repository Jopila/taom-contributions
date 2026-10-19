using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="ITownBuildingsAdapter"/>. Ids are compared as strings, never by instance.</summary>
public sealed class TownBuildingsAdapter : ITownBuildingsAdapter
{
    public IReadOnlyList<FiefSnapshot> Fortifications()
    {
        var result = new List<FiefSnapshot>();
        // Town.AllFiefs walks the campaign's town list, then its castle list; Town.AllTowns holds towns only.
        if (Campaign.Current == null)
            return result;

        foreach (var town in Town.AllFiefs)
        {
            var settlement = town?.Settlement;
            if (settlement?.StringId == null)
                continue;

            var typeIds = new List<string>();
            foreach (var building in town!.Buildings)
            {
                if (building?.BuildingType?.StringId is { } id)
                    typeIds.Add(id);
            }

            result.Add(new FiefSnapshot(settlement.StringId, settlement.Culture?.StringId, typeIds));
        }

        return result;
    }

    public int GetLevel(string settlementId, IReadOnlyCollection<string> typeIds)
    {
        var buildings = Find(settlementId)?.Town?.Buildings;
        if (buildings == null)
            return 0;

        var level = 0;
        foreach (var building in buildings)
        {
            if (building?.BuildingType?.StringId is { } id && typeIds.Contains(id) && building.CurrentLevel > level)
                level = building.CurrentLevel;
        }

        return level;
    }

    public void Add(string settlementId, string typeId)
    {
        var town = Find(settlementId)?.Town;
        var type = MBObjectManager.Instance?.GetObject<BuildingType>(typeId);
        if (town == null || type == null)
            return;

        // The same constructor the engine's new-game pass uses (BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart).
        town.Buildings.Add(new Building(type, town, 0f, type.StartLevel));
    }

    // Settlement.Find reads MBObjectManager.Instance without a null check, and a null id throws from its dictionary lookup.
    private static Settlement? Find(string settlementId) =>
        string.IsNullOrEmpty(settlementId) || MBObjectManager.Instance == null ? null : Settlement.Find(settlementId);
}
