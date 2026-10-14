using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Lets each War Chronicle daily listener run at most once per campaign day (whole days, floored, like the
/// ledger's <c>day=</c>). On a load the engine waits only the elapsed part of the day before the next daily
/// tick (Campaign.CreateCampaignEvents, v1.5.5 Campaign.cs:1024-1030), so a save made at fraction f of day S
/// next ticks at S + 2f: day S a second time when f is under a half. Each listener has its own lane, since
/// both run inside one tick: <see cref="TickLane"/> for <c>WarChronicleTickService.RunDaily</c> (effects,
/// baselines, rally, escapes, ledger) and <see cref="ChronicleLane"/> for the chronicle's daily step.
/// The last day run per lane is saved in the payload's <c>daily</c> section; a save without it holds none, so
/// its first tick runs. Reset for a new campaign by <see cref="WarChronicleStateService"/>.
/// </summary>
public sealed class WarChronicleDailyGate
{
    public const string TickLane = "tick";
    public const string ChronicleLane = "chronicle";

    private readonly Dictionary<string, int> _lastRun = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// True, and the day recorded, when <paramref name="day"/> is later than the last day this lane ran.
    /// A positive requirement on the clock: NaN, an infinity, a negative day or one past int range claims
    /// nothing, so a bad clock neither runs the work nor poisons the saved day.
    /// </summary>
    public bool TryClaim(string lane, double day)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(day, 0d, int.MaxValue))
            return false;

        var whole = (int)Math.Floor(day);
        if (_lastRun.TryGetValue(lane, out var last) && whole <= last)
            return false;

        _lastRun[lane] = whole;
        return true;
    }

    public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_lastRun, StringComparer.Ordinal);

    /// <summary>Replaces every lane with the saved days; a lane the save does not name has not run.</summary>
    public void Restore(IReadOnlyDictionary<string, int> days)
    {
        _lastRun.Clear();
        foreach (var pair in days)
            _lastRun[pair.Key] = pair.Value;
    }

    public void ResetForNewSession() => _lastRun.Clear();
}
