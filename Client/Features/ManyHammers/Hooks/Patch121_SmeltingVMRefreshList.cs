using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;

namespace TAOM.Features.ManyHammers.Hooks;

/// <summary>
/// Postfix on <c>SmeltingVM.RefreshList()</c> (v1.5.5 SmeltingVM.cs:189), which builds the smelting list in the constructor,
/// after every smelt, on each switch to the Smelting tab and in BannerlordCoop's client refresh. When "Hide Locked Items
/// from Smelting" is on, it drops the locked rows, clears any selection when no row is left (vanilla's reselect would
/// pass a null on), and moves a locked selection to the first row left of the same weapon, else the first row left.
/// The rows are the view model's own, read through its public surface.
/// </summary>
[HarmonyPatch(typeof(SmeltingVM), nameof(SmeltingVM.RefreshList), new Type[0])]
[HarmonyPatchCategory(ManyHammersModule.PatchCategory)]
public static class Patch121_SmeltingVMRefreshList
{
    [HarmonyPostfix]
    public static void Postfix(SmeltingVM __instance)
    {
        try
        {
            if (ManyHammersCalls.Service.HidesLockedItems) SmeltingListFilter.Apply(__instance);
        }
        catch (Exception ex)
        {
            ManyHammersCalls.Error("RefreshList", ex);
        }
    }
}
