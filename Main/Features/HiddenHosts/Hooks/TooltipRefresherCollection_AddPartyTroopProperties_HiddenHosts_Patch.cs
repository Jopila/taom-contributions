using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). The private builder of a troops
/// or prisoners section appends a blank spacer, the section title (whose value is the count, masked by the
/// <c>GetPartySizeText</c> postfix) and then, for an inspected subject, the formation rows and the per-troop rows. While the
/// frame is masked or at the horizon the postfix removes everything after the title: the prefix records where this call's
/// rows began in a <see cref="TrimToken"/> (default means do nothing; a bare index of 0 could not tell a stripped prefix
/// from a start). Rows are only ever appended, so the trim never touches earlier rows. It hides rows; it never changes what a
/// count call receives. For a masked party or army (a remembered count or not yet), the title row of the troops section (found by the
/// title vanilla passed, never by the row's text) is replaced by a row that runs vanilla's own function with the frame's
/// <c>InTotal</c> flag set, so the one number that is the subject's total shows the "~" count and every other number
/// stays a live reading. Main thread.
/// </summary>
[HarmonyPatch(typeof(TooltipRefresherCollection), "AddPartyTroopProperties")]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch
{
    /// <summary>The spacer and the title row this call adds first, which the trim keeps.</summary>
    private const int KeptRows = 2;

    [HarmonyPrefix]
    public static void Prefix(PropertyBasedTooltipVM propertyBasedTooltipVM, out TrimToken __state)
    {
        __state = default;
        var frame = HiddenHostsTooltipScope.Current;
        if (frame == null || !frame.TrimsTroopRows || HiddenHostsCalls.Stopped) return;
        var rows = propertyBasedTooltipVM?.TooltipPropertyList;
        if (rows == null) return;
        __state = new TrimToken(rows.Count);
    }

    [HarmonyPostfix]
    public static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM, TextObject title, TrimToken __state)
    {
        if (!__state.IsSet) return;
        try
        {
            var rows = propertyBasedTooltipVM?.TooltipPropertyList;
            if (rows == null) return;
            for (int i = rows.Count - 1; i >= __state.Start + KeptRows; i--)
                rows.RemoveAt(i);
            if (rows.Count == __state.Start + KeptRows && IsSubjectTotal(HiddenHostsTooltipScope.Current, title))
                rows[__state.Start + 1] = WrapTotal(rows[__state.Start + 1]);
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch), ex);
        }
    }

    /// <summary>This call is the troops section (not the prisoners) of a masked party or army, whose total row may show a remembered count.</summary>
    private static bool IsSubjectTotal(TooltipFrame? frame, TextObject title) =>
        frame != null && frame.WrapsTotalRow && title != null && title.ToString() == HiddenHostsCalls.TroopsLabel();

    /// <summary>
    /// The same row with the same label, height, flags and extended settings, whose value function marks the frame as being in
    /// the total while vanilla's original function runs (public API only: the original row keeps vanilla's function, and its
    /// refresh is the one call). The flag is cleared in a finally, so a throwing roster cannot leave it set. It is evaluated
    /// once in the new row's constructor, inside the frame this postfix runs in, and again at every two-second refresh.
    /// </summary>
    private static TooltipProperty WrapTotal(TooltipProperty original)
    {
        // Gauntlet ignores MBBindingList item replacement, so after an Alt rebuild the widget may stay bound to the original
        // row. That is why ReadTotal refreshes the original row (the widget's own object) instead of calling its function.
        var wrapped = new TooltipProperty(original.DefinitionLabel, () => ReadTotal(original), original.TextHeight,
            original.OnlyShowWhenExtended, (TooltipProperty.TooltipPropertyFlags)original.PropertyModifier);
        wrapped.OnlyShowWhenNotExtended = original.OnlyShowWhenNotExtended;
        return wrapped;
    }

    private static string ReadTotal(TooltipProperty original)
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame != null) frame.InTotal = true;
        try
        {
            original.RefreshValue();
            return original.ValueLabel;
        }
        finally
        {
            if (frame != null) frame.InTotal = false;
        }
    }
}
