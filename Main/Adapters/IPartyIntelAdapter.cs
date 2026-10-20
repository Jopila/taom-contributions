using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the campaign's party, faction and visibility data for Hidden Hosts (#846, ADR-007): one read turns a
/// <c>MobileParty</c> into the plain <see cref="PartyIntelSnapshot"/> the service decides on.
/// </summary>
public interface IPartyIntelAdapter
{
    /// <summary>The snapshot of <paramref name="party"/> against the main party, or <see cref="PartyIntelSnapshot.NoData"/> when either is missing.</summary>
    PartyIntelSnapshot Read(MobileParty? party);

    /// <summary>The snapshot of <paramref name="settlement"/> against the main party, or <see cref="SettlementIntelSnapshot.NoData"/> when either is missing.</summary>
    SettlementIntelSnapshot ReadSettlement(Settlement? settlement);

    /// <summary>The snapshot of <paramref name="mapEvent"/> against the main party, or <see cref="EventIntelSnapshot.NoData"/> when either is missing.</summary>
    EventIntelSnapshot ReadMapEvent(MapEvent? mapEvent);

    /// <summary>The snapshot of <paramref name="siegeEvent"/> against the main party, or <see cref="EventIntelSnapshot.NoData"/> when either is missing.</summary>
    EventIntelSnapshot ReadSiegeEvent(SiegeEvent? siegeEvent);

    /// <summary>
    /// Fills <paramref name="into"/> (cleared first) with a light snapshot of every party in plain sight of the main party: in
    /// its current settlement, the one it encounters or talks to, in its map event, or in its own siege on either side. A
    /// party is added only when its own <see cref="PartyIntelSnapshot.InPlainSight"/> is true, the one definition the mask
    /// uses. The light read carries only what an exact stamp needs (id, counts, visibility, the own-side and hidden facts, the
    /// clock and the scout), not the spotting ratio or the war check. Returns the snapshot of the settlement the main party is
    /// inside, or <see cref="SettlementIntelSnapshot.NoData"/>.
    /// </summary>
    SettlementIntelSnapshot CollectPlainSight(List<PartyIntelSnapshot> into);
}
