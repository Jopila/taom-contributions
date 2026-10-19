using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan fast open (docs/reference/harmony-patch-registry.md "PatchXXX_ShadowfaxClan"). A void prefix opens a refresh scope
/// for this <c>ClanPartiesVM</c> and hands the token to the finalizer through <c>__state</c>, so the list build vanilla makes
/// inside <c>RefreshValues</c> can be skipped by <see cref="ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch"/>. The void
/// finalizer closes the scope; after a skip it re-renders the two list headers, because vanilla sets those counts only at the
/// end of the list build (ClanPartiesVM.cs:500-503) and <c>RefreshValues</c> has just reset them to the bare text. Void, so
/// Harmony keeps <c>rethrow</c> and the engine's stack. <c>First</c>, so PatchShield cannot swallow the exception before it
/// is seen here. <c>object __instance</c> so a test dummy of the same shape binds.
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), nameof(ClanPartiesVM.RefreshValues))]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
public static class ClanPartiesVM_RefreshValues_ClanFastOpen_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(object __instance, out object? __state)
    {
        __state = null;
        try { __state = ClanFastOpenCalls.Service?.EnterRefresh(__instance); }
        catch { /* EnterRefresh never throws; this guards the call itself */ }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(object __instance, Exception? __exception, object? __state)
    {
        try
        {
            var skipped = ClanFastOpenCalls.Service?.ExitRefresh(__state) ?? false;
            if (skipped && __exception == null && __instance is ClanPartiesVM vm) RenderHeaders(vm);
        }
        catch { /* a header count is cosmetic: it must never change the engine's outcome */ }
    }

    // Exactly vanilla's tail of RefreshPartiesList (ClanPartiesVM.cs:500-503), over the lists the earlier build left. The
    // Refuge postfix (Patch75) appended its rows to Garrisons in that build, so the count here includes them, as vanilla's did.
    private static void RenderHeaders(ClanPartiesVM vm)
    {
        GameTexts.SetVariable("CURRENT", vm.Caravans.Count);
        vm.CaravansText = GameTexts.FindText("str_clan_caravans").ToString();
        GameTexts.SetVariable("CURRENT", vm.Garrisons.Count);
        vm.GarrisonsText = GameTexts.FindText("str_clan_garrisons").ToString();
    }
}
