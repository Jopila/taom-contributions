using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan fast open (docs/reference/harmony-patch-registry.md "PatchXXX_ShadowfaxClan"). A bool prefix that returns false only
/// when <see cref="ClanScreenFastPathService.ShouldSkipListBuild"/> says this call is the one inside the same instance's
/// <c>RefreshValues</c>; every other call (the constructor's, the screen's category refresh, a co-op handler) builds. When it
/// builds it passes its start stamp to the void finalizer through <c>__state</c> (0 means "not timed"), which adds the
/// build to the open window's count. The Refuge and TroopWeight postfixes on this method are Harmony postfixes: they run
/// only for a build that runs, which is the build whose rows they decorate. Void finalizer, <c>First</c>.
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshPartiesList))]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
public static class ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(object __instance, out long __state)
    {
        __state = 0;
        try
        {
            var service = ClanFastOpenCalls.Service;
            if (service == null) return true;
            if (service.ShouldSkipListBuild(__instance)) return false;
            __state = service.BeginBuild();
        }
        catch { /* a fault here lets vanilla build */ }
        return true;
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(long __state)
    {
        try { ClanFastOpenCalls.Service?.EndBuild(__state); }
        catch { /* a count must never change the engine's outcome */ }
    }
}
