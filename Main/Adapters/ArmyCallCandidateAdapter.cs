using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.RedArrow;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IArmyCallCandidateAdapter"/> over the live campaign (v1.5.5, #873). Reads what
/// <c>DefaultArmyManagementCalculationModel.CheckPartyEligibility</c> (:405-479) reads, in its order, with the same
/// expressions: the ruler comparison keeps vanilla's null equality, the army comparison is against
/// <c>Hero.MainHero.PartyBelongedTo?.Army</c>, the size score, size ratio and distance limit come from the active
/// <c>ArmyManagementCalculationModel</c> exactly as vanilla reads them, and "disbanding" includes the disband behavior's
/// waiting list. The travel facts follow vanilla's reads: the naval capability only while the player's party is at sea and the
/// distance only when neither the sea nor the raft check applies. A fact vanilla would not have read stays at its refusing
/// value (no naval capability, NaN distance).
/// </summary>
public sealed class ArmyCallCandidateAdapter : IArmyCallCandidateAdapter
{
    public ArmyCallCandidate Read(MobileParty? party)
    {
        if (party == null)
            return new ArmyCallCandidate(false, false, CandidateArmy.None, false, 0f, 0f, false, false, false, false, float.NaN, float.NaN);

        var model = Campaign.Current.Models.ArmyManagementCalculationModel;
        var hero = Hero.MainHero;
        var ownArmy = hero.PartyBelongedTo?.Army;
        var army = party.Army == null ? CandidateArmy.None
            : party.Army == ownArmy ? CandidateArmy.PlayersArmy
            : CandidateArmy.OtherArmy;
        var settlement = party.CurrentSettlement;
        var main = MobileParty.MainParty;
        var atSea = main.IsCurrentlyAtSea;
        var inRaft = party.IsInRaftState;

        return new ArmyCallCandidate(
            hasParty: true,
            isFactionLeaderParty: party.LeaderHero == hero.MapFaction?.Leader,
            army: army,
            isFightingOrBesieged: party.MapEvent != null || party.SiegeEvent != null || (settlement != null && settlement.IsUnderSiege),
            sizeScore: model.GetPartySizeScore(party),
            minSizeScore: model.PlayerMobilePartySizeRatioToCallToArmy,
            isDisbanding: IsDisbanding(party),
            mainPartyAtSea: atSea,
            hasNavalCapability: atSea && party.HasNavalNavigationCapability,
            isInRaftState: inRaft,
            distance: atSea || inRaft ? float.NaN : DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(party, main, party.NavigationCapability, out _),
            maxDistance: model.MaximumDistanceToCallToArmy);
    }

    // Vanilla: IsDisbanding, or the disband behavior has the party on its waiting list. A missing behavior is "not waiting".
    private static bool IsDisbanding(MobileParty party)
    {
        if (party.IsDisbanding)
            return true;
        var behavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
        return behavior != null && behavior.IsPartyWaitingForDisband(party);
    }
}
