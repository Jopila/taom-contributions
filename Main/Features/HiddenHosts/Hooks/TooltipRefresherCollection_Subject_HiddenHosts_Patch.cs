using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). The bracket around the five
/// map-tooltip builders vanilla registers for a party, an army, a settlement, a battle and a siege. The prefix opens a
/// frame for the subject in <c>args[0]</c> (the verdict for its distance) and hands the token back as Harmony's
/// <c>__state</c>; the postfix, still inside the frame, renames the title of a party or army at its horizon and rewrites a
/// settlement's defenders row, and adds the "Counted ... ago" row when the total row showed a remembered count (<c>ShowedMemory</c>); the
/// finalizer closes the frame, always, with no toggle or budget gate in front of it (a stripped prefix leaves the token
/// default, and closing twice writes the same previous frame). Main thread.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class TooltipRefresherCollection_Subject_HiddenHosts_Patch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshMobilePartyTooltip));
        yield return AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshArmyTooltip));
        yield return AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        yield return AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshMapEventTooltip));
        yield return AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshSiegeEventTooltip));
    }

    [HarmonyPrefix]
    public static void Prefix(object[] args, out ScopeToken __state)
    {
        __state = default;
        __state = HiddenHostsTooltipFrames.Open(args != null && args.Length > 0 ? args[0] : null, nameof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
    }

    [HarmonyPostfix]
    public static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM)
    {
        var frame = HiddenHostsTooltipScope.Current;
        var service = HiddenHostsCalls.Service;
        if (frame == null || service == null || HiddenHostsCalls.Stopped || propertyBasedTooltipVM?.TooltipPropertyList == null) return;
        try
        {
            if (frame.RenamesTitle) RenameTitle(propertyBasedTooltipVM);
            if (frame.RewritesDefenders) RewriteDefenders(propertyBasedTooltipVM, service, frame);
            if (frame.AddsReasonRow) propertyBasedTooltipVM.AddProperty(string.Empty, ReasonText);
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(TooltipRefresherCollection_Subject_HiddenHosts_Patch), ex);
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(ScopeToken __state) => HiddenHostsTooltipScope.Close(in __state);

    /// <summary>
    /// "Counted 5 hours ago", the value of the reason row (added only when a remembered count was printed). The row's function
    /// captures nothing: at every refresh it reads the frame that is current then, so the age moves on and the row goes blank
    /// once the player is close enough to read the count again.
    /// </summary>
    private static string ReasonText()
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame == null || frame.Verdict.MemoryText == null || HiddenHostsCalls.Stopped) return string.Empty;
        try { return HiddenHostsCalls.CountedAgo(frame.Verdict.MemoryHours); }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(TooltipRefresherCollection_Subject_HiddenHosts_Patch), ex);
            return string.Empty;
        }
    }

    /// <summary>Both party builders add the title first, flagged <c>Title</c>; found by position and flag, never by its text.</summary>
    private static void RenameTitle(PropertyBasedTooltipVM tooltip)
    {
        var rows = tooltip.TooltipPropertyList;
        if (rows.Count == 0) return;
        var first = rows[0];
        if ((first.PropertyModifier & (int)TooltipProperty.TooltipPropertyFlags.Title) == 0) return;
        first.ValueLabel = HiddenHostsTexts.UnknownParty;
    }

    /// <summary>
    /// The defenders row is found first, by the text of the game id vanilla gave it, and only then is the text asked for: a
    /// tooltip without the row (the extended view, a hideout, hidden information) neither changes nor claims a remembered
    /// count. Its value is a plain string, so a periodic refresh never rewrites it.
    /// </summary>
    private static void RewriteDefenders(PropertyBasedTooltipVM tooltip, HiddenHostsService service, TooltipFrame frame)
    {
        var rows = tooltip.TooltipPropertyList;
        var label = HiddenHostsCalls.DefendersLabel();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].DefinitionLabel != label) continue;
            var text = service.DefendersText(frame.Verdict);
            if (text == null) return;
            rows[i].ValueLabel = text;
            frame.ShowedMemory = frame.Verdict.MemoryText != null && text == frame.Verdict.MemoryText;
            return;
        }
    }
}
