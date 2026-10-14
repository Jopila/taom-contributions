using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Reads the campaign's kingdoms and clock for the War Chronicle (ledger, baselines, rally) so the
/// services never touch <c>Kingdom</c>, <c>Clan</c> or <c>CampaignTime</c> (ADR-007).
/// </summary>
public interface IKingdomWarSnapshotAdapter
{
    /// <summary>One snapshot per non-eliminated kingdom; empty with no campaign.</summary>
    IReadOnlyList<KingdomWarSnapshot> GetKingdoms();

    /// <summary>The campaign's unique id (<c>Campaign.UniqueGameId</c>); empty when there is none.</summary>
    string GetCampaignId();

    /// <summary>Whole days since the campaign start, floored; 0 when unknown. A line label only: a decision
    /// (the daily gate, the load marker) uses <see cref="TryGetElapsedDay"/>, which keeps an invalid clock
    /// apart from a real day 0.</summary>
    int GetElapsedDay();

    /// <summary>
    /// Whole days since the campaign start, floored. False, with <paramref name="day"/> 0, when there is no
    /// campaign or the engine clock is NaN, infinite, negative or past int range.
    /// </summary>
    bool TryGetElapsedDay(out int day);

    /// <summary>Campaign time in hours (<c>CampaignTime.Now.ToHours</c>); 0 with no campaign.</summary>
    double GetNowHours();
}
