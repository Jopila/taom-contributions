using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.KingsMuster;

namespace TAOM.Features.CulturalFeats.Models;

/// <summary>
/// Thin GameModel boundary — converts <see cref="MobileParty"/> into the
/// adapter the <see cref="ICulturalFeatsService"/> needs, then delegates. Per
/// <c>gamemodels.md</c> rule 4: no inline if/foreach/switch/yield, no business
/// logic. Issues #144 / #176. Also the seam for the King's Muster (#874): the ruler's call-cost discount, decided by
/// <see cref="KingsMusterService"/> from two facts <see cref="RulerFactsReader"/> reads.
/// </summary>
public class TaomArmyManagementModel : DefaultArmyManagementCalculationModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly KingsMusterService _muster;

    public TaomArmyManagementModel(ICulturalFeatsService feats)
    {
        _feats = feats;
        // Resolved once here, not as a constructor parameter: that would need an edit to SubModule.cs, where this model is built.
        _muster = IoC.Resolve<KingsMusterService>();
    }

    // Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
    // vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
    // Replaces the prior Owner-only inline that skipped LeaderHero.Culture (the Codex-review-43
    // systemic gap the other party-culture models were already migrated for).
    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
        => _feats.ApplyArmyInfluenceAward(
            CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
            base.DailyBeingAtArmyInfluenceAward(armyMemberParty));

    // The King's Muster discount wraps the culture feats' result, so it applies after them (KingsMusterWiringTests pins the order).
    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
        => _muster.ApplyRulerDiscount(
            RulerFactsReader.IsKingdomRuler(armyLeaderParty),
            RulerFactsReader.IsLedByMainHero(armyLeaderParty),
            _feats.ApplyArmyInfluenceCost(
                CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
                base.CalculatePartyInfluenceCost(armyLeaderParty, party)));
}
