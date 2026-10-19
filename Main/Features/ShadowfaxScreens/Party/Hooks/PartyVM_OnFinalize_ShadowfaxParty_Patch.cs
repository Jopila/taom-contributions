using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>Ends the Party Fast Open session when the party screen's view model is finalized. Idempotent. UI main thread.</summary>
[HarmonyPatch(typeof(PartyVM), nameof(PartyVM.OnFinalize))]
[HarmonyPatchCategory("PatchXXX_ShadowfaxParty")]
public static class PartyVM_OnFinalize_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            PartyFastOpenCalls.Service?.EndScreen();
        }
        catch
        {
            // Vanilla's finalize runs regardless.
        }
    }
}
