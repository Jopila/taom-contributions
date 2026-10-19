using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.TaomBuildings;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IBuildingTypeRegistrarAdapter"/>. Mirrors how the engine builds its own types
/// (DefaultBuildingTypes.Create): <c>RegisterPresumedObject(new BuildingType(id))</c>, then <c>Initialize</c>. Production
/// costs are indexed by the level being built (index 0 pays for level 1), the start level is 0, the variance chance 0
/// (the seeding service, not the new-game roll, decides where a building appears) and the effect list is empty: the
/// building's effect is applied by TAOM's own model, not by a <c>BuildingEffectEnum</c>.
/// </summary>
public sealed class BuildingTypeRegistrarAdapter : IBuildingTypeRegistrarAdapter
{
    public void RegisterAll(IEnumerable<TaomBuildingDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            foreach (var variant in definition.Variants)
            {
                var type = Game.Current.ObjectManager.RegisterPresumedObject(new BuildingType(variant.Id));
                type.Initialize(
                    new TextObject(variant.Name),
                    new TextObject(variant.Explanation),
                    new[] { definition.Costs[0], definition.Costs[1], definition.Costs[2] },
                    Array.Empty<Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>>(),
                    definition.IsMilitary,
                    0f);
            }
        }
    }
}
