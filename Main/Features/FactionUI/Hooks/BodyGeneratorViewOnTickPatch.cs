using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade.View;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: asks native again, every tick, for the resources of each face generator model
/// still loading. Vanilla asks once, when it builds the model (<c>CheckResources(true, true)</c> in
/// <c>RefreshCharacterEntityAux</c>), and only checks afterwards (<c>CheckResources(false, true)</c> in
/// <c>OnTick</c>). When the faction screen's 3D preview is torn down during the face generator's first
/// frames, that one request is lost and the model never becomes visible (2026-10-28, the kingdoms
/// without a painted portrait). Vanilla's own <c>CharacterTableau.OnTick</c> asks on every tick;
/// this gives the face generator the same recovery. A prefix, so vanilla's check sees the result in
/// the same tick. The list empties once a model shows, so a loaded screen pays nothing.
/// </summary>
[HarmonyPatch(typeof(BodyGeneratorView), nameof(BodyGeneratorView.OnTick))]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
[ClientOnly]
public static class BodyGeneratorViewOnTickPatch
{
    // Resolved on first use, never in a static initializer: a renamed field must reach the catch below,
    // not throw a TypeInitializationException out of every OnTick.
    private static FieldInfo? _visualsBeingPrepared;
    private static bool _reported;

    static void Prefix(BodyGeneratorView __instance)
    {
        try
        {
            _visualsBeingPrepared ??= AccessTools.Field(typeof(BodyGeneratorView), "_visualsBeingPrepared");
            if (_visualsBeingPrepared?.GetValue(__instance) is not List<KeyValuePair<AgentVisuals, int>> preparing)
                return;
            foreach (var entry in preparing)
                entry.Key?.GetEntity()?.CheckResources(true, true);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(BodyGeneratorViewOnTickPatch), ex);
        }
    }
}
