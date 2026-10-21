using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the campaign for Far Watch (#847, ADR-007): the clock, the camp and party facts, whether the hero a ride sent
/// out is still with the party, the parties in sight, the towns and castles near, a settlement's defenders, whether a party
/// encounter involves the main party, and the Scouting XP write. It reports what the campaign says and decides nothing: who may
/// ride, who is included in a report and what ends a ride are the service's. Plain ids, numbers and strings go out; no engine
/// object is kept. Main thread only.
/// </summary>
public interface IFarWatchWorldAdapter
{
    /// <summary>The campaign clock in hours, or -1 when there is no campaign or the clock is not finite.</summary>
    double NowHours { get; }

    ScoutCampFacts ReadCamp();

    /// <summary>
    /// The hero is still in the main party, alive and not a prisoner; false when he left, died or was taken. Heroes are looked up
    /// by id among the living heroes, never through the object manager.
    /// </summary>
    bool IsHeroWithParty(string heroId);

    /// <summary>
    /// Fills <paramref name="into"/> (cleared first) with every active, visible party in the main party's seeing range,
    /// with the fog facts Hidden Hosts reads for it. The main party itself is among them: the service leaves it out.
    /// </summary>
    void CollectSightings(List<ScoutSighting> into);

    /// <summary>Fills <paramref name="into"/> (cleared first) with every town and castle on the map and its offset from the main party.</summary>
    void CollectFiefs(List<ScoutSite> into);

    /// <summary>The defenders of a settlement by party, with the snapshot Hidden Hosts remembers; <c>Found</c> is false for an unknown id.</summary>
    ScoutSiteReport ReadSite(string settlementId);

    /// <summary>Adds raw Scouting XP to the hero (learning rate and focus apply, as for vanilla's own XP). False when he cannot be found.</summary>
    bool GrantScoutingXp(string heroId, float xp);

    /// <summary>The main party is in a map event.</summary>
    bool IsMainPartyInMapEvent { get; }

    /// <summary>The party encounter that was just raised (<c>CampaignEvents.OnPartyEncounterEvent</c>) has the main party as its attacker or its defender.</summary>
    bool InvolvesMainParty(PartyBase attacker, PartyBase defender);
}
