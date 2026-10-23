namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// Hold the Walls (#871): the entry points of <c>HoldTheWallsBehavior</c>. Single-player only; every call stands down in a co-op
/// session, under a co-op mod TAOM cannot probe and on a dedicated server. Main thread only.
/// </summary>
public interface IHoldTheWallsService
{
    /// <summary>
    /// Forgets every capture and hold without touching the engine. Called from the behavior's constructor, which the
    /// feature module runs for every campaign (csharp-architecture.md, the session-reset rule).
    /// </summary>
    void ResetForNewSession();

    /// <summary>
    /// A settlement changed owner. A siege capture of a town or castle is recorded for the next hourly pass; a second
    /// siege capture of the same fortress replaces the record. Every other change is ignored here, and so is the hold
    /// the fortress had: the next hourly pass releases it when its army no longer belongs to the realm that holds the
    /// fortress.
    /// </summary>
    void OnSettlementOwnerChanged(string? settlementId, bool isFortification, bool bySiege);

    /// <summary>One pass: starts the holds of new captures, ends the holds that must end, drops the records that ended on their own.</summary>
    void OnHourlyTick();

    /// <summary>
    /// The engine disbanded an army for inactivity (<c>Army.ArmyDispersionReason.Inactivity</c>). Counted for the daily
    /// line whether or not the feature is on, so a run with the switch off is the baseline; the player's own army and
    /// anything outside single-player are ignored. Writes nothing to the engine.
    /// </summary>
    void OnInactivityDisband(bool isPlayersArmy);

    /// <summary>Writes the daily <c>[HoldTheWalls]</c> debug line and zeroes the day's counters.</summary>
    void LogDailySummary();
}
