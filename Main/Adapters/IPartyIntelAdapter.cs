using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the campaign's party, faction and visibility data for Hidden Hosts (#846, ADR-007): one read turns a
/// <c>MobileParty</c> into the plain <see cref="PartyIntelSnapshot"/> the service decides on.
/// </summary>
public interface IPartyIntelAdapter
{
    /// <summary>The snapshot of <paramref name="party"/> against the main party, or <see cref="PartyIntelSnapshot.NoData"/> when either is missing.</summary>
    PartyIntelSnapshot Read(MobileParty? party);
}
