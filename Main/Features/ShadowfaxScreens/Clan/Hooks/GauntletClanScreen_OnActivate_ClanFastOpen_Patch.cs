using System;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan screen open timer (docs/reference/harmony-patch-registry.md "PatchXXX_ShadowfaxClan"). <c>GauntletClanScreen</c>
/// implements <c>IGameStateListener.OnActivate</c> explicitly, so the target is found through the interface map, never a
/// name string. A void prefix opens a <c>[ScreenOpen] Clan open</c> window (null while "Log Screen Open Times" is off, which
/// also keeps the service's count window closed); the void finalizer closes both and writes the line with the list-build
/// counts as its detail. Timing only: it changes nothing the screen does.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
public static class GauntletClanScreen_OnActivate_ClanFastOpen_Patch
{
    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
    {
        var map = typeof(GauntletClanScreen).GetInterfaceMap(typeof(IGameStateListener));
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
            if (map.InterfaceMethods[i].Name == nameof(IGameStateListener.OnActivate)) return map.TargetMethods[i];
        return null;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ScreenOpenWindow? __state)
    {
        __state = null;
        try
        {
            __state = ScreenOpenTiming.Begin("Clan", "open");
            if (__state != null) ClanFastOpenCalls.Service?.BeginOpen();
        }
        catch { /* a diagnostic must never break the screen it measures */ }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, ScreenOpenWindow? __state)
    {
        try
        {
            if (__state == null) return;
            ScreenOpenTiming.End(__state, __exception, ClanFastOpenCalls.Service?.EndOpen());
        }
        catch { /* observe-only */ }
    }
}
