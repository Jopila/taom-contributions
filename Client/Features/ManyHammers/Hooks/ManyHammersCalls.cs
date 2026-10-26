using System;
using System.Collections.Generic;
using TaleWorlds.InputSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.ManyHammers.Hooks;

/// <summary>
/// What the three Many Hammers patches share: the service (resolved on first use and kept), the held-key reads and a log line
/// that says each failure once. The smithy has no TAOM input seam, so the keys are read straight from the engine's input,
/// left and right together.
/// </summary>
internal static class ManyHammersCalls
{
    private static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);
    private static ManyHammersService? _service;

    internal static ManyHammersService Service => _service ??= IoC.Resolve<ManyHammersService>();

    internal static bool ShiftHeld => Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);

    internal static bool CtrlHeld => Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);

    /// <summary>Writes the first failure of each (source, exception type) in full; repeats are dropped.</summary>
    internal static void Error(string source, Exception ex)
    {
        lock (Logged)
        {
            if (!Logged.Add(source + ":" + ex.GetType().FullName)) return;
        }

        try
        {
            IoC.Resolve<IModLogger>()?.LogError($"[ManyHammers] {source} failed (repeats suppressed): {ex}");
        }
        catch
        {
            /* logger unresolvable, nothing to do */
        }
    }
}
