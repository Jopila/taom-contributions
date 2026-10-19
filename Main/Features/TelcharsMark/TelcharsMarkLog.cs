using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.TelcharsMark;

/// <summary>
/// Error logging for the Telchar's Mark hooks and mixins: the first failure of each (source, exception type)
/// is written in full; repeats are dropped, because the filter postfix runs once per row per filter pass.
/// </summary>
internal static class TelcharsMarkLog
{
    private static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);
    private static IModLogger? _logger;

    internal static void Error(string source, Exception ex)
    {
        lock (Logged)
        {
            if (!Logged.Add(source + ":" + ex.GetType().FullName)) return;
        }
        try
        {
            _logger ??= IoC.Resolve<IModLogger>();
            _logger?.LogError($"[TelcharsMark] {source} failed (repeats suppressed): {ex}");
        }
        catch { /* logger unresolvable, nothing to do */ }
    }
}
