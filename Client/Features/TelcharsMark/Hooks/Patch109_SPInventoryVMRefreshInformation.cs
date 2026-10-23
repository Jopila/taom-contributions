using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.TelcharsMark.UI;

namespace TAOM.Features.TelcharsMark.Hooks;

/// <summary>
/// Postfix on the private <c>SPInventoryVM.RefreshInformationValues()</c> (v1.5.5 SPInventoryVM.cs:3968). Every
/// vanilla command handler ends with it (equip, unequip, buy, sell, slaughter, donate, transfer-all: :3482-:3727,
/// :4838), and so do the outfit setter (:1647) and a character switch (:4315), so this rescores once per vanilla
/// action. During the constructor the screen mixin does not exist yet, so the open-time rescore is the mixin's.
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), "RefreshInformationValues")]
[HarmonyPatchCategory("Patch109_TelcharsMark")]
public static class Patch109_SPInventoryVMRefreshInformation
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            UpgradeScreenMixin.Find(__instance)?.Rescore("RefreshInformationValues");
        }
        catch (Exception ex)
        {
            TelcharsMarkLog.Error("RefreshInformationValues", ex);
        }
    }
}
