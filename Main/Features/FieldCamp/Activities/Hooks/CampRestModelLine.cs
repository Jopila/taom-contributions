using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// The healing model's boundary for rest healing in camp (#860): reads the engine facts, lets <see cref="CampRestHealing.Grant"/>
/// decide, then adds the labelled line (a base amount that vanilla's usual healing percentages raise too) and the two lodging
/// perks through vanilla's own helper and flags, so no perk value is guessed. Kept out of the model so it stays a thin entry point.
/// </summary>
internal static class CampRestModelLine
{
    public static ExplainedNumber Apply(CampRestHealing rest, PartyBase party, bool isPrisoners, bool forHeroes, ExplainedNumber result)
    {
        var mobile = party?.MobileParty;
        if (mobile == null)
            return result;

        var grant = rest.Grant(party == Campaign.Current?.MainParty?.Party, isPrisoners, party.IsStarving,
            mobile.CurrentSettlement != null, forHeroes);
        if (grant.Bonus > 0f)
            result.Add(grant.Bonus, new TextObject("{=taom_camp_rest_label}Resting in camp"));
        if (grant.LodgingPerks)
        {
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Athletics.AGoodDaysRest, mobile, isPrimaryBonus: true, ref result);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Medicine.GoodLogdings, mobile, isPrimaryBonus: true, ref result);
        }
        return result;
    }
}
