using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.ManyHammers;

namespace TAOM.Adapters;

/// <summary>What the player's party holds of each of the nine crafting materials, in the engine's slot order.</summary>
internal static class CraftBenchStock
{
    internal static int[] Read(SmithingModel model)
    {
        var roster = MobileParty.MainParty.ItemRoster;
        var stock = new int[UnitSnapshot.Slots];
        for (var slot = 0; slot < stock.Length; slot++)
        {
            var material = model.GetCraftingMaterialItem((CraftingMaterials)slot);
            stock[slot] = material == null ? 0 : roster.GetItemNumber(material);
        }

        return stock;
    }
}
