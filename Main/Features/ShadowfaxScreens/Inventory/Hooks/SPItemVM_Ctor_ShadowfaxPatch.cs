using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TAOM.Features.ShadowfaxScreens.Inventory.Hooks;

// Verified on the installed v1.5.5: the 8-argument SPItemVM constructor reads `Concept.All` (the cached
// `Campaign.Current.Concepts` list) and calls `SingleOrDefault<Concept>` twice, into `_tradeGoodConceptObj` and
// `_itemConceptObj`, which only ExecuteConcept reads. ConceptLookupRewrite swaps those two calls for a memo keyed by
// call site and source list; any other IL shape leaves vanilla. No global Concept.All patch.

[HarmonyPatch(typeof(SPItemVM), MethodType.Constructor, new[]
{
    typeof(InventoryLogic), typeof(bool), typeof(bool), typeof(InventoryScreenHelper.InventoryMode),
    typeof(ItemRosterElement), typeof(InventoryLogic.InventorySide), typeof(int), typeof(EquipmentIndex?),
})]
[HarmonyPatchCategory(InventoryPatchCategory.Name)]
public static class SPItemVM_Ctor_ShadowfaxPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = ConceptLookupRewrite.Rewrite(instructions.ToList(), ShadowfaxInventoryCalls.Logger, out var swapped);
        ShadowfaxInventoryCalls.Service?.ReportSite("concept lookup", swapped, ConceptLookupRewrite.ExpectedSites);
        return result;
    }
}
