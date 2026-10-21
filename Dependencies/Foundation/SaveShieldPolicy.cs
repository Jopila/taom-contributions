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
    /// Otherwise the only change from historic behaviour is the SAVE-LOAD and MISSION-INIT chains
    /// during a co-op session. Swallowing SAVE-LOAD produces a partially deserialised campaign that
    /// keeps running and looks fine; a host-authoritative co-op mod then replicates that half-built
    /// state to the other player. A loud load failure is much cheaper than two silently-corrupted
    /// saves. MISSION-INIT is rethrown too: a swallowed fault leaves a half-initialised mission that
    /// the co-op mod's mission sync then treats as real, so the peers disagree about the battle with
    /// no error. Singleplayer keeps swallowing both, since there a broken battle is only local.
    /// </summary>
    public static bool ShouldSwallow(string? category, bool coopActive, bool swallowDisabledByFlag)
    {
        if (swallowDisabledByFlag) return false;
        if (!coopActive) return true;

        return !string.Equals(category, SaveLoadCategory, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(category, MissionInitCategory, StringComparison.OrdinalIgnoreCase);
    }
}
