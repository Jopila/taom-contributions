using System;
using Helpers;
using HarmonyLib;
using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). Every troop total and count row of
/// the map tooltips prints through <c>PartyBaseHelper.GetPartySizeText(int, int, bool)</c>. Inside an open frame this postfix
/// replaces the text with the known leading digits and "?" (no wounded part, as on the nameplate), or "..." at the horizon;
/// for the subject's own troop total (the frame's <c>InTotal</c> flag, set by the wrapper around that row) it shows the
/// remembered "~" count when the verdict carries one; outside a frame it reads one static and returns. A call vanilla made with <c>isInspected == false</c> already prints
/// vanilla's own "???" and is left alone, so Hidden Hosts never shows more than vanilla. The overload is pinned by its
/// parameter types: a <c>PartyBase</c> overload exists. Main thread (another thread sees no frame).
/// </summary>
[HarmonyPatch(typeof(PartyBaseHelper), nameof(PartyBaseHelper.GetPartySizeText), new[] { typeof(int), typeof(int), typeof(bool) })]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch
{
    [HarmonyPostfix]
    public static void Postfix(int healtyNumber, int woundedNumber, bool isInspected, ref TextObject __result)
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame == null || !isInspected || frame.Verdict.Mode == TooltipMode.Vanilla) return;
        var service = HiddenHostsCalls.Service;
        if (service == null || HiddenHostsCalls.Stopped) return;
        try
        {
            var text = frame.InTotal
                ? service.MaskTooltipTotal(frame.Verdict, healtyNumber)
                : service.MaskTooltipCount(frame.Verdict, healtyNumber);
            if (frame.InTotal && text != null && text == frame.Verdict.MemoryText) frame.ShowedMemory = true;
            if (text != null) __result = new TextObject("{=!}" + text);
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch), ex);
        }
    }
}
