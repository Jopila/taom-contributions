using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.BanditVoices.Hooks;

/// <summary>
/// Patch122, #900: a postfix on private <c>bandit_neutral_greet_on_condition</c> (no public seam sets the greeting), faulting
/// to vanilla's text; callers, thread and co-op notes: docs/reference/harmony-patch-registry.md "Patch122_BanditVoices".
/// </summary>
[HarmonyPatch(typeof(BanditInteractionsCampaignBehavior), "bandit_neutral_greet_on_condition")]
[HarmonyPatchCategory(BanditVoicesModule.PatchCategory)]
public static class BanditGreetingPatch
{
    private static bool _reported;
    private static IModLogger? _logger;

    internal static BanditVoicesService? Service { get; private set; }

    internal static void Initialize(BanditVoicesService service, IModLogger logger)
    {
        Service = service;
        _logger = logger;
        _reported = false;
    }

    [HarmonyPostfix]
    public static void Postfix(bool __result)
    {
        if (!__result)
            return;

        try
        {
            Service?.Apply();
        }
        catch (Exception ex)
        {
            if (_reported)
                return;
            _reported = true;
            _logger?.LogWarning("[BanditVoices] clan greeting failed, vanilla's text stands: " + ex.Message);
        }
    }
}
