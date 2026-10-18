using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Features.Diplomacy.Models;

public class TaomDiplomacyModel : DefaultDiplomacyModel
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly ICoopSessionProvider _coop;
    private readonly IOnAllianceAction _allianceHook;

    public TaomDiplomacyModel(
        IWarOfTheRingService wotrService, ICoopSessionProvider coop, IOnAllianceAction allianceHook)
    {
        _wotrService = wotrService;
        _coop = coop;
        _allianceHook = allianceHook;
    }

    /// <summary>
    /// #370 — the THIRD site reading <c>ShouldBlockPeace</c>, and the one the review agent itself
    /// missed while reporting the other two. "Constantly at war" makes peace permanently
    /// unreachable between two factions, so a peer whose WotR phase had drifted would hold a
    /// different view of which wars can ever end. Same gate as the prefixes and the permission
    /// model: under co-op the host's diplomacy governs.
    /// </summary>
    public override bool IsAtConstantWar(IFaction faction1, IFaction faction2)
    {
        if (!_coop.ShouldDeferToHost
            && _wotrService.IsWarOfTheRingActive
            && _wotrService.ShouldBlockPeace(faction1.StringId, faction2.StringId))
        {
            return true;
        }

        return base.IsAtConstantWar(faction1, faction2);
    }

    /// <summary>
    /// The AI pays 200 influence per war proposal, and a proposal the permission model refuses is then
    /// cancelled, so a forbidden war must never score well enough to be proposed. The penalty is a
    /// constant per veto class (see <see cref="IWarOfTheRingService.GetDeclareWarScorePenalty"/>), so
    /// the order among equally vetoed neighbours survives, which the Mercenary start uses to pick its
    /// enemy. Kingdom against kingdom only, and off under co-op; the hook owns both conditions.
    /// </summary>
    public override float GetScoreOfDeclaringWar(
        IFaction factionDeclaresWar, IFaction factionDeclaredWar, Clan evaluatingClan,
        out TextObject reason, bool includeReason = false)
    {
        return base.GetScoreOfDeclaringWar(
                factionDeclaresWar, factionDeclaredWar, evaluatingClan, out reason, includeReason)
            - _allianceHook.GetDeclareWarScorePenalty(
                factionDeclaresWar.StringId, factionDeclaresWar.IsKingdomFaction,
                factionDeclaredWar.StringId, factionDeclaredWar.IsKingdomFaction);
    }

    public override int GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
        Hero supporter, bool hasHeroVotedAgainstOwner)
    {
        int result = base.GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
            supporter, hasHeroVotedAgainstOwner);

        if (hasHeroVotedAgainstOwner
            && supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)
        {
            result += (int)(result * TaomCulturalFeats.IsengardDecisionPenaltyFeat.EffectBonus);
        }

        return result;
    }
}
