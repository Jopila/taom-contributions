using System;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The swallow decision behind <see cref="SaveShield"/>, extracted as a pure function so the full
/// input matrix can be tested without Harmony or a running game.
/// </summary>
public static class SaveShieldPolicy
{
    /// <summary>Category tag on SaveShield's save/load-chain targets.</summary>
    public const string SaveLoadCategory = "SAVE-LOAD";

    /// <summary>Category tag on SaveShield's mission-init-chain targets.</summary>
    public const string MissionInitCategory = "MISSION-INIT";

    /// <summary>
    /// Whether the finalizer should swallow the exception (return null) or let it propagate.
    ///
    /// <paramref name="swallowDisabledByFlag"/> dominates: <c>saveshield-swallow-disabled.flag</c>
    /// is the blunt "rethrow everything" escalation used during triage, and nothing overrides it.
    ///
    /// Otherwise the only change from historic behaviour is the SAVE-LOAD chain during a co-op
    /// session. Swallowing there produces a partially deserialised campaign that keeps running and
    /// looks fine; a host-authoritative co-op mod then replicates that half-built state to the
    /// other player. A loud load failure is much cheaper than two silently-corrupted saves.
    ///
    /// MISSION-INIT keeps swallowing in every case, co-op included, because a rethrow does not
    /// surface the fault, it breaks the engine. SpawnTroop and SetMissionMode can run inside
    /// <c>Mission.OnMissionTick</c>, where a rethrow skips the agent tick that sets
    /// <c>tickCompleted</c>, so the next frame's <c>OnPreTick</c> waits forever and the game hangs
    /// (v1.5.5 <c>Mission.cs:3749</c>, <c>:3594-3599</c>). A rethrow out of
    /// <c>MissionState.OnInitialize</c> leaves an inactive state on top of the state stack. The
    /// loading-time rethrows leave the mission as half-initialised as the swallow does, so they buy
    /// nothing either.
    /// </summary>
    public static bool ShouldSwallow(string? category, bool coopActive, bool swallowDisabledByFlag)
    {
        if (swallowDisabledByFlag) return false;
        if (!coopActive) return true;

        return !string.Equals(category, SaveLoadCategory, StringComparison.OrdinalIgnoreCase);
    }
}
