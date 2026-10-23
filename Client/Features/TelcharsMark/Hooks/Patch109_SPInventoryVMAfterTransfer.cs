using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.TelcharsMark.UI;

namespace TAOM.Features.TelcharsMark.Hooks;

/// <summary>
/// Postfix on the private <c>SPInventoryVM.AfterTransfer(InventoryLogic, List&lt;TransferCommandResult&gt;)</c>
/// (v1.5.5 SPInventoryVM.cs:3838), raised once per transfer command. Every vanilla handler brackets its commands
/// with IsRefreshed=false ... RefreshInformationValues(); IsRefreshed=true (:3480-3484, :3492-3495, :3522-3531,
/// :3561-3570, :3708-3717, :3723-3732, :4751-4840), so the RefreshInformationValues postfix covers those once and
/// this one returns early while IsRefreshed is false. It covers the transfers nothing follows with that call:
/// TAOM's EquipPresets load (InventoryScreenAdapter AddTransferCommands) and InventoryLogic.Reset, whose empty
/// AfterTransfer (InventoryLogic.cs:734-739) runs after AfterReset rebuilt the slots (:3796, :3806).
/// </summary>
[HarmonyPatch(typeof(SPInventoryVM), "AfterTransfer", new[] { typeof(InventoryLogic), typeof(List<TransferCommandResult>) })]
[HarmonyPatchCategory("Patch109_TelcharsMark")]
public static class Patch109_SPInventoryVMAfterTransfer
{
    [HarmonyPostfix]
    public static void Postfix(SPInventoryVM __instance)
    {
        try
        {
            if (!__instance.IsRefreshed) return;
            UpgradeScreenMixin.Find(__instance)?.Rescore("AfterTransfer");
        }
        catch (Exception ex)
        {
            TelcharsMarkLog.Error("AfterTransfer", ex);
        }
    }
}
