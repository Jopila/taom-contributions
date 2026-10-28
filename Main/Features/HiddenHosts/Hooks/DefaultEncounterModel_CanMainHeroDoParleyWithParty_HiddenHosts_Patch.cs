using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", field parley, #1037). A postfix on
/// <c>DefaultEncounterModel.CanMainHeroDoParleyWithParty</c>, the one question the parley icon, the "Ctrl + click" hint and
/// the click itself all ask (v1.5.6: the party and settlement nameplate flags, the tooltip hint row and
/// <c>MapScreen.TryParleyWith</c>). A host with no name on the map (<see cref="HiddenHostsService.MasksParley"/>, the plate's own
/// verdict, for a party drawn on the map) is refused, and the explanation vanilla built, which names the party, is dropped: the
/// caller (<c>MapScreen.TryParleyWith</c>) then prints its own "Cannot parley with this party.", which names nobody. Vanilla's
/// answer stays for everything else: the toggle off, a settlement, a host the scout named or one in plain sight, a refusal that
/// carried no text, an error, or the fault budget spent. The nameplate view asks once per party plate per frame while Ctrl is
/// held, so the common exits read only the arguments and the settings flag. Main thread.
/// </summary>
[HarmonyPatch(typeof(DefaultEncounterModel), nameof(DefaultEncounterModel.CanMainHeroDoParleyWithParty))]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
public static class DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch
{
    [HarmonyPostfix]
    public static void Postfix(PartyBase partyBase, ref bool __result, ref TextObject explanation)
    {
        // A refusal with no text has nothing to hide, and vanilla's fallback line names no party.
        if (!__result && explanation == null) return;
        var service = HiddenHostsCalls.Service;
        var adapter = HiddenHostsCalls.Adapter;
        if (HiddenHostsCalls.Stopped || service == null || adapter == null || !service.Enabled) return;
        try
        {
            var party = partyBase?.MobileParty;
            if (party == null || !service.MasksParley(adapter.Read(party))) return;

            __result = false;
            explanation = null!;
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch), ex);
        }
    }
}
