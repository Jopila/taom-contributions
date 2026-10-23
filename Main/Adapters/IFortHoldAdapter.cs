using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Every engine call Hold the Walls makes (#871, ADR-007): reading the armies inside a captured fortress and pausing or
/// resuming a leader's AI. Ids in, plain values out; each id is resolved against the live campaign on every call, so
/// nothing here outlives a save load. It reads and writes only the AI pause (<c>MobilePartyAi.DisableForHours</c> and
/// <c>EnableAi</c>, the two calls the engine itself makes on a capturing army); every decision is the service's. Main thread only.
/// </summary>
public interface IFortHoldAdapter
{
    /// <summary>Campaign time in hours; NaN when there is no campaign.</summary>
    double CampaignHours { get; }

    /// <summary>
    /// Every army whose leader party stands inside the fortress, whoever leads it and whichever realm it serves. The
    /// service decides which of them hold. Empty when the id names no town or castle.
    /// </summary>
    IReadOnlyList<FortArmyReading> ReadArmiesIn(string fortId);

    /// <summary>One leader read against one fortress, or <see cref="FortArmyReading.Missing"/> when it no longer leads an army.</summary>
    FortArmyReading ReadArmy(string leaderPartyId, string fortId);

    /// <summary>Pauses the leader's AI for <paramref name="hours"/> (the engine saves and expires the pause itself). A missing or inactive party is a no-op.</summary>
    void HoldForHours(string leaderPartyId, int hours);

    /// <summary>Lets the leader's AI think again at once. A missing or inactive party is a no-op.</summary>
    void ResumeAi(string leaderPartyId);
}
