using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Composition;

/// <summary>
/// Runs one step of an engine lifecycle hook that has no try of its own. Catches everything, logs one
/// WARNING per step name per process, and never rethrows. The step is not latched: it runs again on
/// the next call, so it must be safe to repeat. Engine-free and unit-tested.
/// </summary>
internal static class LifecycleStep
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> Logged = new(StringComparer.Ordinal);

    internal static bool Run(string name, Action step, Func<IModLogger?> logger)
    {
        try
        {
            step();
            return true;
        }
        catch (Exception ex)
        {
            Report(name, ex, logger);
            return false;
        }
    }

    internal static void ResetForTests()
    {
        lock (Gate) Logged.Clear();
    }

    private static void Report(string name, Exception ex, Func<IModLogger?> logger)
    {
        try
        {
            lock (Gate)
            {
                if (!Logged.Add(name)) return;
            }

            logger()?.LogWarning(
                $"[MainMenuStep] step '{name}' failed and was skipped; it runs again on the next call, " +
                $"further failures are not logged: {ex.GetType().Name}: {ex.Message}");
        }
        catch
        {
            // A failing logger must never turn a skipped step into a hook throw.
        }
    }
}
