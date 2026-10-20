using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.CrownGuard.Hooks;

/// <summary>
/// Patch112, #825: postfix on <c>CampaignSceneNotificationHelper.GetBodyguardOfCulture</c> (public static, v1.5.5), which
/// the become-king scene calls for the two guards of the new ruler's kingdom culture. The target has two engine callers:
/// that scene (raised only when the player wins a king election) and Story Mode's conspiracy scenes
/// (<c>EmpireConspiracySupportsSceneNotificationItemBase</c>), which TAOM's hidden Story Mode never reaches. Vanilla switches on its own
/// culture ids and returns a Sturgian fighter for any other, so a TAOM kingdom is guarded by the wrong people. The postfix
/// replaces the result with the troop mapped for the culture, and leaves vanilla's result when the culture has no row or
/// the mapped troop is not loaded (one warning per culture id). The scene tints a guard with its own troop culture's colours, so a guard borrowed from
/// another culture is given the crowned culture's colours instead. Notes: docs/reference/harmony-patch-registry.md "Patch112_CrownGuard".
/// </summary>
[HarmonyPatch(typeof(CampaignSceneNotificationHelper), nameof(CampaignSceneNotificationHelper.GetBodyguardOfCulture))]
[HarmonyPatchCategory(CrownGuardModule.PatchCategory)]
public static class CampaignSceneNotificationHelper_GetBodyguardOfCulture_CrownGuard_Patch
{
    private static readonly HashSet<string> WarnedCultures = new HashSet<string>();

    private static CrownGuardConfigProvider? _config;
    private static IModLogger? _logger;

    [HarmonyPostfix]
    public static void Postfix(ref SceneNotificationData.SceneNotificationCharacter __result, CultureObject culture)
    {
        _config ??= IoC.Resolve<CrownGuardConfigProvider>();
        var cultureId = culture?.StringId;
        var troopId = _config.GetMap().GuardTroopId(cultureId);
        if (troopId == null)
            return;

        var troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        if (troop != null)
        {
            __result = new SceneNotificationData.SceneNotificationCharacter(troop, customColor1: culture!.Color, customColor2: culture.Color2);
            return;
        }

        if (WarnedCultures.Add(cultureId!))
        {
            _logger ??= IoC.Resolve<IModLogger>();
            _logger.LogWarning($"CrownGuard: the guard troop '{troopId}' for culture '{cultureId}' is not loaded, keeping the vanilla guard");
        }
    }
}
