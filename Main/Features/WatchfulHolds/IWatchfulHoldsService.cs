namespace TAOM.Features.WatchfulHolds;

public interface IWatchfulHoldsService
{
    /// <summary>
    /// One party's hourly check. The handle is the engine party, passed as <see cref="object"/> so the service
    /// names no TaleWorlds type (ADR-007); only <c>IWatchfulHoldsAdapter</c> looks inside it.
    /// </summary>
    void OnHourlyTickParty(object engineParty);

    /// <summary>Logs one debug line with the day's checks, triggers and slowest hour, then resets the counters.</summary>
    void LogDailySummary();

    /// <summary>Forgets the horn clock, the counters and the cached fort list; the behavior's constructor calls it for every campaign.</summary>
    void ResetForNewSession();
}
