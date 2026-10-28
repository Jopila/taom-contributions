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
    private const int GiveUpTicks = 300;
    private static readonly AccessTools.FieldRef<BodyGeneratorView, object> VisualToShow =
        AccessTools.FieldRefAccess<BodyGeneratorView, object>("_visualToShow");
    private static readonly AccessTools.FieldRef<BodyGeneratorView, Equipment> DressedEquipment =
        AccessTools.FieldRefAccess<BodyGeneratorView, Equipment>("_dressedEquipment");

    private static BodyGeneratorView? _watched;
    private static int _ticks;
    private static int _opens;

    static bool Prepare() => typeof(BodyGeneratorView).GetConstructors().Length == 1;

    static MethodBase TargetMethod() => typeof(BodyGeneratorView).GetConstructors()[0];

    static void Postfix(BodyGeneratorView __instance)
    {
        try
        {
            _opens++;
            _watched = __instance;
            _ticks = 0;
            var player = CharacterObject.PlayerCharacter;
            var bodyGen = __instance.BodyGen;
            TableauDiagnostics.LogAlways(
                $"[FaceGenOpen #{_opens}] playerCulture='{player?.Culture?.StringId}' " +
                $"char='{bodyGen?.Character?.StringId}' race={bodyGen?.Race} female={bodyGen?.IsFemale} " +
                $"characterGear=[{Describe(bodyGen?.Character?.Equipment)}] " +
                $"dressedGear=[{Describe(DressedEquipment(__instance))}]");
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
                if (!ReferenceEquals(__instance, _watched))
                    return;
                _ticks++;
                if (VisualToShow(__instance) != null)
                {
                    TableauDiagnostics.LogAlways($"[FaceGenOpen #{_opens}] model visible after {_ticks} tick(s).");
                    _watched = null;
                }
                else if (_ticks >= GiveUpTicks)
                {
                    TableauDiagnostics.LogAlways(
                        $"[FaceGenOpen #{_opens}] model NEVER visible after {_ticks} ticks: a resource never loaded.");
                    _watched = null;
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
