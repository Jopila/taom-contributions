using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips, #848). The party and army map
/// tooltips print a party's orders through <c>CampaignUIHelper.GetMobilePartyBehaviorText</c>, a one-line wrapper over
/// <c>MobileParty.GetBehaviorText()</c>. Inside an open party or army frame whose verdict is not vanilla this postfix
/// replaces the text with "Orders unknown."; the orders show only when that tooltip's own count is read in full or the party
/// is exempt. It never evaluates the party: the frame's verdict is the one that drew the counts. Outside a frame (the menu
/// rows, the clan and kingdom screens, another thread) it reads one static and returns, allocating nothing. A fault keeps
/// vanilla's text. Main thread (another thread sees no frame).
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.GetBehaviorText))]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class MobileParty_GetBehaviorText_HiddenHosts_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref TextObject __result)
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame == null || !frame.HidesOrders || HiddenHostsCalls.Stopped) return;
        try
        {
            __result = HiddenHostsTexts.OrdersUnknown();
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(MobileParty_GetBehaviorText_HiddenHosts_Patch), ex);
        }
    }
}
