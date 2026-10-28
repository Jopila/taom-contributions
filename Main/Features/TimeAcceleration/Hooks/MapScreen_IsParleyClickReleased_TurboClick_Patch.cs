using System;
using HarmonyLib;
using SandBox.View.Map;
using TAOM.Core.Logging;

namespace TAOM.Features.TimeAcceleration.Hooks;

/// <summary>
/// Patch124 (docs/reference/harmony-patch-registry.md "Patch124_TurboClick", #1040). v1.5.6 MapScreen.HandleMouse sends a
/// released click with a party or settlement under the cursor to HandleParleyClick, not the move handler, when the private
/// IsParleyClickReleased() is true, and on a keyboard that means LeftControl (MapParleyModifier) is held. TAOM's turbo is
/// Ctrl + Space, so a Left Ctrl turbo click on a party or settlement went to the parley handler instead of moving
/// (a friendly or neutral target printed "Cannot parley with this party.", an at-war target printed vanilla's
/// refusal or opened a parley). While the turbo latch is open this postfix answers false, and
/// HandleMouse's own else branch handles the click as v1.5.5 did (move, follow, attack). Without turbo vanilla's answer
/// stands. HandleMouse asks every map frame; the answer is false on all but a Ctrl click release, and that exit reads
/// nothing. A fault is logged once and vanilla's answer stands. Main thread.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "IsParleyClickReleased")]
[HarmonyPatchCategory(TimeAccelerationModule.PatchCategory)]
[ClientOnly]
public static class MapScreen_IsParleyClickReleased_TurboClick_Patch
{
    private static ITimeAccelerationService? _service;
    private static IModLogger? _logger;
    private static bool _faultLogged;

    // TAOM.Tests sets both caches. In game the service is resolved on the first true answer and the logger on the first fault.
    internal static void Initialize(ITimeAccelerationService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
        _faultLogged = false;
    }

    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (!__result) return;
        try
        {
            if ((_service ??= IoC.Resolve<ITimeAccelerationService>()).IsTurboActive)
                __result = false;
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void ReportOnce(Exception ex)
    {
        if (_faultLogged) return;
        _faultLogged = true;
        try
        {
            (_logger ??= IoC.Resolve<IModLogger>())?.LogError(
                $"[TimeAcceleration] Patch124 turbo click postfix threw; vanilla's parley answer stands: {ex}");
        }
        catch
        {
            // Never throw out of a postfix that HandleMouse calls every map frame.
        }
    }
}
