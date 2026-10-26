using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TAOM.Core.Logging;

namespace TAOM.Features.SeasonedHosts.Hooks;

/// <summary>
/// Patch118, #890 (docs/reference/harmony-patch-registry.md "Patch118_SeasonedHostsStewardXp"). A bool prefix on
/// <c>DefaultSkillLevelingManager.OnSettlementProjectFinished(Settlement)</c>: false skips the Steward XP grant. Every other
/// outcome, a fault included, returns true and vanilla runs.
/// </summary>
[HarmonyPatch(typeof(DefaultSkillLevelingManager), nameof(DefaultSkillLevelingManager.OnSettlementProjectFinished))]
[HarmonyPatchCategory(SeasonedHostsModule.PatchCategory)]
public static class Patch118_ProjectStewardXp
{
    private static SeasonedHostsService? _service;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(SeasonedHostsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _logged = false;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        var service = _service;
        if (service == null)
            return true;

        try
        {
            return !service.SkipsProjectStewardXp();
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger?.LogError($"[SeasonedHosts] Patch118 prefix failed, vanilla grants the Steward XP: {ex}");
            }

            return true;
        }
    }
}
