using System;
using System.Collections.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TAOM.Core.Logging;

namespace TAOM.Features.ReevesLedger.UI;

/// <summary>
/// The guard around every member the engine calls into the ledger (constructor, refresh, click, hover), with one log line
/// per call site per window. A throw out of any of them reaches no TAOM log by itself, and a call site that fails once usually
/// fails every time, so the latch keeps the file to one line per site; a different site still logs. A guarded hover function
/// returns an empty result: it runs inside the engine's hover dispatch, where an escaping exception takes the hint system down.
/// </summary>
internal sealed class LedgerFaultLog
{
    private readonly IModLogger _logger;
    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    internal LedgerFaultLog(IModLogger logger) => _logger = logger;

    /// <summary>Runs <paramref name="action"/>; a throw is logged once for <paramref name="site"/> and swallowed.</summary>
    internal void Run(string site, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
        }
    }

    internal List<TooltipProperty> Lines(string site, Func<List<TooltipProperty>> build)
    {
        try
        {
            return build();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
            return new List<TooltipProperty>();
        }
    }

    internal string Text(string site, Func<string> build)
    {
        try
        {
            return build();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
            return "";
        }
    }

    internal void LogOnce(string site, Exception ex)
    {
        if (!_logged.Add(site))
            return;
        _logger.LogError($"[ReevesLedger] {site} threw. This line is logged once per window and later calls still run. Root cause: {ex}");
    }

    internal void LogOnce(string site, string problem)
    {
        if (!_logged.Add(site))
            return;
        _logger.LogError($"[ReevesLedger] {site}: {problem}");
    }
}
