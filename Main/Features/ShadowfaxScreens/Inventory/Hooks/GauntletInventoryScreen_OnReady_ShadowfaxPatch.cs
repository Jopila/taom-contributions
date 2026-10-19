using System;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Inventory.Hooks;

// Verified on the installed v1.5.5: protected override void GauntletInventoryScreen.OnReady() builds the view model
// and loads the "Inventory" movie for both the inventory and the trade screen, and returns early when
// `_gauntletLayer != null` (a repeat OnReady), so only the call that sees a null layer is an open. Observe only:
// the prefix returns nothing, the void finalizer keeps Harmony's rethrow, and `__state` is the only link between
// them (a stripped prefix leaves it null and the finalizer does nothing).

[HarmonyPatch(typeof(GauntletInventoryScreen), "OnReady")]
[HarmonyPatchCategory(InventoryPatchCategory.Name)]
public static class GauntletInventoryScreen_OnReady_ShadowfaxPatch
{
    [HarmonyPrefix]
    public static void Prefix(GauntletLayer ____gauntletLayer, out ScreenOpenWindow? __state)
        => __state = ____gauntletLayer == null ? ShadowfaxInventoryCalls.BeginOpen() : null;

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, ScreenOpenWindow? __state)
        => ShadowfaxInventoryCalls.EndOpen(__state, __exception);
}
