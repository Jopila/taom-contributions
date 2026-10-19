using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.HiddenHosts;

/// <summary>The service and adapter Patch105 reaches, handed over by the module's static initialisation, and the hooks' fault budget.</summary>
internal static class HiddenHostsCalls
{
    /// <summary>The faults the hooks tolerate before they stop for the rest of the process.</summary>
    internal const int FaultBudget = 32;

    private static readonly HashSet<string> Reported = new();
    private static int _faults;

    internal static HiddenHostsService? Service { get; private set; }

    internal static IPartyIntelAdapter? Adapter { get; private set; }

    private static IModLogger? Logger { get; set; }

    /// <summary>True once the fault budget is spent: every hook returns at once, leaving vanilla's text.</summary>
    internal static bool Stopped => _faults >= FaultBudget;

    internal static void Initialize(HiddenHostsService? service, IPartyIntelAdapter? adapter, IModLogger? logger = null)
    {
        Service = service;
        Adapter = adapter;
        Logger = logger;
        Reported.Clear();
        _faults = 0;
    }

    /// <summary>
    /// A hook that failed shows vanilla's text for that frame. A fault that repeats every frame would flood the log and
    /// cost a throw per plate, so one line per hook and exception type goes to the log, never one per frame, and the
    /// <see cref="FaultBudget"/>th fault stops the hooks (one "from now on" line); the hooks read <see cref="Stopped"/>.
    /// </summary>
    internal static void ReportFault(string hook, Exception ex)
    {
        bool wasSpent = _faults >= FaultBudget;
        if (!wasSpent) _faults++;
        bool spent = _faults >= FaultBudget;
        try
        {
            if (Reported.Add(hook + ": " + ex.GetType().Name))
                Logger?.LogError($"[HiddenHosts] {hook} fell back to vanilla: {ex}");
            if (spent && !wasSpent)
                Logger?.LogError($"[HiddenHosts] {FaultBudget} faults: the hooks are off from now on, vanilla counts show until the game restarts.");
        }
        catch
        {
            // The log itself failed; vanilla text is already showing.
        }
    }
}
