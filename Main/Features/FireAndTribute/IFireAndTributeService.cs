using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The raiding hosts' rules (#870), as the behavior and the console see them. The spawn, the steering and the console spawn
/// stand down unless the game is single-player (no co-op session, no co-op mod TAOM cannot probe, no dedicated server).
/// The village and destroy handlers are not gated: they find nothing to act on, because the records are emptied when a
/// session goes live.
/// </summary>
public interface IFireAndTributeService
{
    /// <summary>Clears every per-campaign field. The behavior's constructor calls it for each campaign start.</summary>
    void ResetForNewSession();

    /// <summary>The save record as one JSON string.</summary>
    string CaptureState();

    /// <summary>Restores a save record over the reset; null, empty or damaged content leaves what it cannot read reset.</summary>
    void LoadFrom(string? json);

    /// <summary>After a load: drops hosts the engine lost, releases those a co-op session took over, and retires those whose switch is off.</summary>
    void OnSessionLaunched();

    /// <summary>Steers every live host (one per side) one step; a host whose step throws is logged and the rest still steer.</summary>
    void OnHourlyTick();

    /// <summary>The spawn roll for each side, then the daily debug line.</summary>
    void OnDailyTick();

    /// <summary>A village changed state; <paramref name="looted"/> is true for the Looted state. Records only.</summary>
    void OnVillageStateChanged(bool looted, string? raiderPartyId);

    void OnPartyDestroyed(string? partyId);

    /// <summary>The console's status lines.</summary>
    IReadOnlyList<string> Describe();

    /// <summary>
    /// The console's spawn: sends the side's host now, ignoring the timers, the chance and the cooldown. A dry run
    /// reports the plan and changes nothing.
    /// </summary>
    string TrySpawn(FactionSide side, bool dryRun);
}
