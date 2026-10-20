using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Features.FellFoes;

/// <summary>
/// Runs the Fell Foes passes for ONE mission: once a second it asks the gate whether the mission is still
/// eligible, takes a scan, then runs the cornered, archer and straggler passes, each in its own try and gated by
/// its own toggle, so one throwing or switched off leaves the others running. A fresh instance is built per
/// mission, so its state is the mission's and nothing carries into the next battle. Main thread only: the owning
/// mission behavior calls it from <c>OnMissionTick</c> and <c>ReleaseHolds</c> at mission end.
/// A turned (cornered) soldier is deliberately never un-turned on a switch-off, a failure, a closed gate or at
/// mission end: an unsteady at the end would turn a capture into a rout, and under co-op no write is allowed.
/// </summary>
public sealed class FellFoesService
{
    private const float PassSeconds = 1f;

    private readonly IFellFoesAdapter _adapter;
    private readonly IFellFoesSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly Func<float> _roll;
    private readonly Func<bool> _isOpen;
    private readonly FellFoesMissionState _state = new FellFoesMissionState();

    private float _accumulated;
    private bool _warnedAboutTunables;
    private bool _scanFailureLogged;
    private bool _corneredFailed;
    private bool _archersFailed;
    private bool _stragglersFailed;
    private int _stopped;
    private int _unsteadied;
    private int _released;
    private int _sentOff;

    public FellFoesService(
        IFellFoesAdapter adapter, IFellFoesSettingsProvider settings, IModLogger logger, Func<float> roll, Func<bool> isOpen)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _isOpen = isOpen ?? throw new ArgumentNullException(nameof(isOpen));
    }

    public void Tick(float dt)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt <= 0f) return;
        _state.Advance(dt);
        _accumulated += dt;
        if (_accumulated < PassSeconds) return;
        _accumulated = 0f;

        // The gate is read here, once a second, behind the validated delta. A closed gate frees every hold first.
        if (!_isOpen())
        {
            ReleaseHolds();
            return;
        }

        RunPasses();
    }

    /// <summary>Gives every held archer his automatic target choice back. Idempotent; call at mission end.</summary>
    public void ReleaseHolds()
    {
        if (_state.Holds.Count == 0) return;
        foreach (var handle in _state.TakeHolds())
            ReleaseOne(handle);
    }

    // Each handle in its own try: one archer that cannot be released never strands the rest.
    private void ReleaseOne(object handle)
    {
        try
        {
            _adapter.ReleaseTarget(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FellFoes] could not release an archer: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void RunPasses()
    {
        var t = _settings.Tunables;
        if (!_warnedAboutTunables)
        {
            _warnedAboutTunables = true;
            foreach (var warning in t.Warnings)
                _logger.LogWarning(warning);
        }

        var cornered = _settings.CorneredRoutersEnabled && !_corneredFailed;
        var archers = _settings.ArchersTargetPlayerEnabled && !_archersFailed;
        var stragglers = _settings.StragglersLeaveFieldEnabled && !_stragglersFailed;

        // The state transition comes before the toggle gate: switching archers off mid-battle (or a failed archer
        // pass) frees every hold on the next pass.
        if (!archers) ReleaseHolds();
        if (!cornered && !archers && !stragglers) return;

        // Health history is only valid across consecutive passes: after a pause (switched off, or failed) the next pass is a first sighting.
        if (!cornered) _state.ClearHealth();

        var scan = TryScan(cornered, archers, t);
        if (scan == null) return;

        _stopped = _unsteadied = _released = _sentOff = 0;
        if (cornered && !Run("cornered", () => Cornered(scan, t))) _corneredFailed = true;
        if (archers && !Run("archer", () => Archers(scan, t))) _archersFailed = true;
        if (stragglers && !Run("straggler", () => Stragglers(scan, t))) _stragglersFailed = true;
        if (_settings.DebugEnabled) LogDebug(scan);
    }

    private FellFoesScan? TryScan(bool cornered, bool archers, FellFoesTunables t)
    {
        try
        {
            return _adapter.Scan(new FellFoesScanRequest
            {
                CorneredRadius = cornered ? t.CorneredRadius : 0f,
                HoldRadius = cornered ? t.HoldRadius : 0f,
                ArcherReadRange = archers ? t.ArcherFieldRange : 0f,
                Cornered = cornered ? (ICollection<object>)_state.Cornered : Array.Empty<object>(),
                Held = _settings.DebugEnabled ? (ICollection<object>)_state.Holds.Keys : Array.Empty<object>(),
            });
        }
        catch (Exception ex)
        {
            ReleaseHolds();
            if (!_scanFailureLogged)
            {
                _scanFailureLogged = true;
                _logger.LogError($"[FellFoes] scan failed, passes skipped: {ex.GetType().Name}: {ex.Message}");
            }

            return null;
        }
    }

    private bool Run(string pass, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            // Never take a battle down over a tweak. The pass stays off for the rest of this mission.
            _logger.LogError($"[FellFoes] {pass} pass disabled for this mission after {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void Cornered(FellFoesScan scan, FellFoesTunables t)
    {
        var stop = new List<object>();
        var reflee = new List<object>();
        FellFoesRules.PlanCornered(scan, _state, t, stop, reflee);
        foreach (var handle in stop)
        {
            if (_adapter.StopRout(handle))
            {
                _stopped++;
                if (_settings.DebugEnabled) NoteStop(handle);
            }
            else
            {
                _state.Cornered.Remove(handle);
            }
        }

        foreach (var handle in reflee)
        {
            if (_adapter.Unsteady(handle)) _unsteadied++;
        }
    }

    private void NoteStop(object handle)
    {
        _state.StopCounts.TryGetValue(handle, out var n);
        _state.StopCounts[handle] = n + 1;
    }

    private void Archers(FellFoesScan scan, FellFoesTunables t)
    {
        var release = new List<object>();
        var hold = new List<object>();
        FellFoesRules.PlanArchers(scan, _state, t, _roll, release, hold);
        foreach (var handle in release)
        {
            ReleaseOne(handle);
            _released++;
        }

        foreach (var handle in hold)
        {
            if (!_adapter.HoldTarget(handle)) _state.Holds.Remove(handle);
        }
    }

    private void Stragglers(FellFoesScan scan, FellFoesTunables t)
    {
        var handle = FellFoesRules.PlanStraggler(scan, _state, t);
        if (handle == null) return;
        if (_adapter.SendOff(handle)) _sentOff++;
        else _state.SentOff.Remove(handle);
    }

    private void LogDebug(FellFoesScan scan)
    {
        var acted = _stopped + _unsteadied + _released + _sentOff;
        if (acted == 0 && _state.Holds.Count == 0) return;

        // Both numbers are over the same archers: the ones whose target this scan read.
        var read = 0;
        var aiming = 0;
        var enemies = scan.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].TargetsPlayer.HasValue) continue;
            read++;
            if (enemies[i].TargetsPlayer == true) aiming++;
        }

        var restopped = 0;
        foreach (var n in _state.StopCounts.Values)
        {
            if (n > 1) restopped++;
        }

        _logger.LogInfo(
            $"[FellFoes] enemies={enemies.Count} peak={_state.PeakEnemies} stopped={_stopped} restopped={restopped} " +
            $"unsteadied={_unsteadied} cornered={_state.Cornered.Count} sentOff={_sentOff} released={_released} " +
            $"held {_state.Holds.Count}, aiming {aiming} of {read} checked");
    }
}
