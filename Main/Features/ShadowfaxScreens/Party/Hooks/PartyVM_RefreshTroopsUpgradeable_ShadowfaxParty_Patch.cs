using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>Opens the category-count window around RefreshTroopsUpgradeable, which counts the same item roster once per row. UI main thread.</summary>
[HarmonyPatch(typeof(PartyVM), "RefreshTroopsUpgradeable")]
[HarmonyPatchCategory("PatchXXX_ShadowfaxParty")]
public static class PartyVM_RefreshTroopsUpgradeable_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out bool __state)
    {
        __state = false;
        try
        {
            __state = PartyFastOpenCalls.Service?.OpenCategoryWindow() ?? false;
        }
        catch
        {
            // Vanilla runs uncached.
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(bool __state, Exception? __exception)
    {
        try
        {
            PartyFastOpenCalls.Service?.CloseCategoryWindow(__state);
        }
        catch
        {
            // Never break the screen.
        }
    }
}
