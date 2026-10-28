using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TAOM.Features.HeroRace.Diagnostics;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// TEMPORARY diagnostic for the empty Character Customization screen on a new kingdom's first open
/// (2026-10-28). Vanilla shows the model only once <c>CheckResources</c> reports every mesh loaded
/// (BodyGeneratorView.OnTick); this logs the gear on the model when the view opens and whether a
/// visual ever became visible. Remove with the fix.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
[ClientOnly]
public static class FaceGenFirstOpenDiagnosticPatch
{
    // Still-hidden reminder; the watch never gives up, so a slow load is not misread as a failed one.
    private const int StillHiddenTicks = 600;

    // Resolved lazily inside the try blocks, never in a static initializer: a renamed engine field
    // would otherwise make every later touch of this class throw TypeInitializationException out of
    // BodyGeneratorView.OnTick each frame.
    private static FieldInfo? _visualToShowField;
    private static FieldInfo? _dressedEquipmentField;

    private static BodyGeneratorView? _watched;
    private static int _ticks;
    private static int _opens;
    private static bool _shown;

    static bool Prepare() => typeof(BodyGeneratorView).GetConstructors().Length == 1;

    static MethodBase TargetMethod() => typeof(BodyGeneratorView).GetConstructors()[0];

    static void Postfix(BodyGeneratorView __instance)
    {
        try
        {
            if (_watched != null && !_shown)
                TableauDiagnostics.LogAlways(
                    $"[FaceGenOpen #{_opens}] closed after {_ticks} tick(s) with the model NEVER visible.");
            _opens++;
            _watched = __instance;
            _ticks = 0;
            _shown = false;
            _dressedEquipmentField ??= AccessTools.Field(typeof(BodyGeneratorView), "_dressedEquipment");
            var player = CharacterObject.PlayerCharacter;
            var bodyGen = __instance.BodyGen;
            TableauDiagnostics.LogAlways(
                $"[FaceGenOpen #{_opens}] playerCulture='{player?.Culture?.StringId}' " +
                $"char='{bodyGen?.Character?.StringId}' race={bodyGen?.Race} female={bodyGen?.IsFemale} " +
                $"characterGear=[{Describe(bodyGen?.Character?.Equipment)}] " +
                $"dressedGear=[{Describe(_dressedEquipmentField?.GetValue(__instance) as Equipment)}]");
        }
        catch (Exception ex)
        {
            TableauDiagnostics.LogError($"[FaceGenOpen] constructor log failed: {ex.GetType().Name} {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(BodyGeneratorView), nameof(BodyGeneratorView.OnTick))]
    [HarmonyPatchCategory(FactionUIPatchContext.Category)]
    [ClientOnly]
    public static class TickWatch
    {
        static void Postfix(BodyGeneratorView __instance)
        {
            try
            {
                if (_shown || !ReferenceEquals(__instance, _watched))
                    return;
                _ticks++;
                _visualToShowField ??= AccessTools.Field(typeof(BodyGeneratorView), "_visualToShow");
                if (_visualToShowField?.GetValue(__instance) != null)
                {
                    _shown = true;
                    TableauDiagnostics.LogAlways($"[FaceGenOpen #{_opens}] model visible after {_ticks} tick(s).");
                }
                else if (_ticks == StillHiddenTicks)
                {
                    TableauDiagnostics.LogAlways(
                        $"[FaceGenOpen #{_opens}] model still hidden after {_ticks} ticks; still watching.");
                }
            }
            catch (Exception ex)
            {
                _watched = null;
                TableauDiagnostics.LogError($"[FaceGenOpen] tick watch failed: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    private static string Describe(Equipment? equipment)
    {
        if (equipment == null)
            return "null";
        return string.Join(", ", Enumerable.Range(0, (int)EquipmentIndex.NumEquipmentSetSlots)
            .Select(i => equipment[(EquipmentIndex)i].Item)
            .Where(item => item != null)
            .Select(item => item.StringId));
    }
}
