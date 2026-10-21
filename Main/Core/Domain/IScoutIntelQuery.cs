namespace TAOM.Core.Domain;

/// <summary>
/// What Hidden Hosts needs to know about Far Watch (docs/features/far-watch.md "Hidden Hosts"), and nothing more: whether the
/// scout is away on a settlement ride, and whether a party is one the scout named. Read-only and cheap, because the nameplate
/// binding asks per plate per frame; Far Watch's store answers from its own plain fields. Hidden Hosts holds this seam, not the
/// feature, so the dependency points one way.
/// </summary>
public interface IScoutIntelQuery
{
    /// <summary>A scout is away on a settlement ride: Hidden Hosts reads the party's Scouting as 0 for how far it reads digits.</summary>
    bool IsScoutAway { get; }

    /// <summary>
    /// The party is one of the hosts the last sweep reported and the scouted sight has not yet ended
    /// (<paramref name="nowHour"/> is the campaign hour; below 0 means no clock and answers false).
    /// </summary>
    bool IsNamedByScout(string partyId, int nowHour);
}
