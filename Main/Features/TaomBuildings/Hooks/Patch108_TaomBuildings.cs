using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Menu.TownManagement;
using TAOM.Core.Logging;

namespace TAOM.Features.TaomBuildings.Hooks;

/// <summary>
/// Patch108, the town screen's building icon (docs/reference/harmony-patch-registry.md "Patch108_TaomBuildings").
/// <c>DevelopmentItemVisualButtonWidget.DetermineSpriteImageFromSpriteCode</c> (private) maps a building type id to a
/// sprite name and returns "" for an id it does not know; the widget then shows <c>building_default</c>. This postfix
/// changes only an empty result for a TAOM id, and only when the sprite exists in the loaded sprite data, so a missing
/// asset leaves the default icon instead of a blank one. The small variant (<c>useSmallVariant</c>) adds <c>_t</c>
/// inside the catalog. Main thread, once per widget and sprite code. Harmony binds the parameters by name. On any fault
/// the engine's result stands and one warning is logged, because every town screen opened asks again.
/// </summary>
[HarmonyPatch(typeof(DevelopmentItemVisualButtonWidget), "DetermineSpriteImageFromSpriteCode")]
[HarmonyPatchCategory(TaomBuildingsModule.PatchCategory)]
[ClientOnly]
public static class Patch108_DevelopmentSprite
{
    private static bool _warned;

    [HarmonyPostfix]
    public static void Postfix(DevelopmentItemVisualButtonWidget __instance, string spriteCode, bool useSmallVariant,
        ref string __result)
    {
        try
        {
            if (!string.IsNullOrEmpty(__result) || string.IsNullOrEmpty(spriteCode))
                return;

            var name = TaomBuildingUiHooks.Hook?.SpriteFor(spriteCode, useSmallVariant);
            if (name != null && __instance.Context.SpriteData.GetSprite(name) != null)
                __result = name;
        }
        catch (Exception ex)
        {
            // The icon falls back to the engine's default. The logger's resolve can fail too, so the log line has a
            // catch of its own.
            if (_warned)
                return;
            _warned = true;
            try { TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TaomBuildings] Patch108 sprite postfix failed ({ex.GetType().Name}: {ex.Message}); the default icon stands"); } catch { }
        }
    }
}

/// <summary>
/// Patch108, the per-level effect line. <c>BuildingType.GetExplanationAtLevel</c> builds the line from the type's
/// <c>BuildingEffectEnum</c> list, which a TAOM building leaves empty, so the engine returns an empty text. This postfix
/// supplies the catalog's line for a TAOM type at level 1 to 3 and leaves every other result alone. On any fault the
/// engine's text stands and one warning is logged.
/// </summary>
[HarmonyPatch(typeof(BuildingType), nameof(BuildingType.GetExplanationAtLevel))]
[HarmonyPatchCategory(TaomBuildingsModule.PatchCategory)]
public static class Patch108_LevelExplanation
{
    private static bool _warned;

    [HarmonyPostfix]
    public static void Postfix(BuildingType __instance, int level, ref TextObject __result)
    {
        try
        {
            var line = TaomBuildingUiHooks.Hook?.LevelText(__instance.StringId, level);
            if (line != null)
                __result = new TextObject(line.Template).SetTextVariable(line.Variable, line.Value);
        }
        catch (Exception ex)
        {
            // The line stays as the engine returns it (empty for a type with no effects); same log shape as above.
            if (_warned)
                return;
            _warned = true;
            try { TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TaomBuildings] Patch108 effect-line postfix failed ({ex.GetType().Name}: {ex.Message}); the default line stands"); } catch { }
        }
    }
}
