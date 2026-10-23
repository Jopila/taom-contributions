using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). The settlement tooltip prints each
/// party inside through <c>CampaignUIHelper.GetPartyNameplateText(MobileParty, bool)</c>; the army menu overlay calls it
/// too, with no frame. The postfix acts only inside a settlement frame: the row takes the settlement's distance, the party's
/// own count, Scouting and realm, and the party's own exemptions. The <c>PartyBase</c> overload is not patched: its only
/// caller prints the main party. Main thread.
/// </summary>
[HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetPartyNameplateText), new[] { typeof(MobileParty), typeof(bool) })]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
[ClientOnly]
public static class CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch
{
    [HarmonyPostfix]
    public static void Postfix(MobileParty party, bool includeAttachedParties, ref string __result)
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame == null || frame.Kind != TooltipSubjectKind.Settlement || frame.Verdict.Mode == TooltipMode.Vanilla) return;
        var service = HiddenHostsCalls.Service;
        var adapter = HiddenHostsCalls.Adapter;
        if (service == null || adapter == null || HiddenHostsCalls.Stopped) return;
        try
        {
            var text = service.MaskSettlementPartyRow(frame.Verdict, adapter.Read(party), includeAttachedParties);
            if (text != null) __result = text;
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch), ex);
        }
    }
}
