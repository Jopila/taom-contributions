using System;
using HarmonyLib;
using SandBox.View.Map.Visuals;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts"). A bool prefix on
/// <c>MobilePartyVisual.OnOpenEncyclopedia</c>, which the map calls when the encyclopedia hotkey (game key 67,
/// MapGoToEncylopedia, unbound on the keyboard by default; Left Option on a controller) is pressed while the cursor is on
/// a party. A party past nine tenths of its own spotting reach has no name on its plate, so the key opens nothing: false
/// skips the original. A host the scout named (#847) keeps its name past the horizon, so its key opens as in vanilla. A left
/// click still engages or escorts as in vanilla. Every other outcome returns true and vanilla runs: the toggle off, a party
/// that is not at its horizon, an error, or the fault budget spent. Main thread.
/// </summary>
[HarmonyPatch(typeof(MobilePartyVisual), nameof(MobilePartyVisual.OnOpenEncyclopedia))]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(MobilePartyVisual __instance)
    {
        var service = HiddenHostsCalls.Service;
        var adapter = HiddenHostsCalls.Adapter;
        if (HiddenHostsCalls.Stopped || __instance == null || service == null || adapter == null || !service.Enabled) return true;
        try { return !service.IsNameHidden(adapter.Read(__instance.MapEntity?.MobileParty)); }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch), ex);
            return true;
        }
    }
}
