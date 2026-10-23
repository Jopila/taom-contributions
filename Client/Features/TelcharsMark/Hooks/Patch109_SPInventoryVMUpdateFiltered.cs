using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.TelcharsMark.UI;

namespace TAOM.Features.TelcharsMark.Hooks;

/// <summary>
/// Postfix on the private <c>SPInventoryVM.UpdateFilteredStatusOfItem(SPItemVM item)</c> (v1.5.5
/// SPInventoryVM.cs:5133), the single place vanilla writes <c>IsFiltered</c>. When "show only better items" is on,
/// a list row vanilla left visible and that has no verdict is hidden. The parameter MUST be named <c>item</c>
/// (Harmony binds by name; pinned by Patch109WiringTests). It runs once per row on every filter pass, so the
/// common cases return after one field read or one weak-table lookup.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), "UpdateFilteredStatusOfItem", new[] { typeof(SPItemVM) })]
[HarmonyPatchCategory("Patch109_TelcharsMark")]
public static class Patch109_SPInventoryVMUpdateFiltered
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance, SPItemVM item)
    {
        try
        {
            if (item == null || item.IsFiltered) return;
            var screen = UpgradeScreenMixin.Find(__instance);
            if (screen == null || !screen.ShowOnlyUpgrades) return;
            var row = UpgradeRowMixin.Find(item);
            if (row != null && TelcharsMarkService.ShouldHide(row, vanillaFiltered: false))
                item.IsFiltered = true;
        }
        catch (Exception ex)
        {
            TelcharsMarkLog.Error("UpdateFilteredStatusOfItem", ex);
        }
    }
}
