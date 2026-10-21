using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.GoodDeeds.Models;

/// <summary>
/// Donating a hero prisoner to a settlement earns a bonus on top of vanilla's relation gain (docs/features/good-deeds.md). Only
/// the hero-relation method is overridden, so the influence from a donated prisoner or troop stays vanilla's. The base value is
/// computed first and handed to the service with the two ids; the service answers vanilla's own value when the feature is off,
/// the facts cannot be read or this peer defers to the co-op host, and otherwise the scaled sum.
/// Vanilla's <c>CharacterRelationCampaignBehavior.OnPrisonerDonatedToSettlement</c> applies the value, for the main party
/// only, to the settlement owner's clan leader, so this model needs no party check and never calls the relation seam.
/// </summary>
public class TaomPrisonerDonationModel : DefaultPrisonerDonationModel
{
    private readonly GoodDeedsService _service;

    public TaomPrisonerDonationModel(GoodDeedsService service)
    {
        _service = service;
    }

    public override float CalculateRelationGainAfterHeroPrisonerDonate(PartyBase donatingParty, Hero donatedHero, Settlement donatedSettlement) =>
        _service.DonationRelation(
            base.CalculateRelationGainAfterHeroPrisonerDonate(donatingParty, donatedHero, donatedSettlement),
            donatedHero?.StringId,
            donatedSettlement?.StringId);
}
