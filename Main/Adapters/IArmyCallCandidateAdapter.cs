using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.RedArrow;

namespace TAOM.Adapters;

/// <summary>
/// Snapshots one party the player might call into the army (#873, ADR-007), in the order vanilla's
/// <c>CheckPartyEligibility</c> reads it. The sea, raft and distance facts are read only as
/// vanilla reads them (see <see cref="ArmyCallCandidate"/>). Main thread only.
/// </summary>
public interface IArmyCallCandidateAdapter
{
    /// <summary>A candidate with <see cref="ArmyCallCandidate.HasParty"/> false for a null party.</summary>
    ArmyCallCandidate Read(MobileParty? party);
}
