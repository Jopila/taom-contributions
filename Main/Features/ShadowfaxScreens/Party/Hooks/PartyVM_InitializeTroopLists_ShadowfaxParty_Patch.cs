using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// Opens the category-count window around InitializeTroopLists (the constructor, Reset and TransferAllTroops all call
/// it) and times it as the open line's "init" phase. The finalizer closes only a window this call opened, learned from
/// __state. UI main thread.
/// </summary>
[HarmonyPatch(typeof(PartyVM), "InitializeTroopLists")]
[HarmonyPatchCategory("PatchXXX_ShadowfaxParty")]
public static class PartyVM_InitializeTroopLists_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out PartyPhaseState __state)
    {
        __state = default;
        try
        {
            var service = PartyFastOpenCalls.Service;
            if (service == null) return;
            __state.OpenedWindow = service.OpenCategoryWindow();
            __state.Start = service.BeginPhase();
        }
        catch
        {
            // Vanilla runs uncached.
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PartyPhaseState __state, Exception? __exception)
    {
        try
        {
            var service = PartyFastOpenCalls.Service;
            if (service == null) return;
            service.CloseCategoryWindow(__state.OpenedWindow);
            service.EndPhase(PartyPhase.InitializeTroopLists, __state.Start);
        }
        catch
        {
            // Never break the screen.
        }
    }
}
