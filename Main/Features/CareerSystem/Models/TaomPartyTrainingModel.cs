using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

/// <summary>
/// Career TroopTraining (#769 E11): flat experience per troop per day, for the leader's own troops of the pip's class.
/// Vanilla's base is the sum of the training perks and, for an AI lord's party only, 10 to 15 plus 2 to 3 per tier; it is
/// 0 for the player's party and clan (<c>DefaultPartyTrainingModel</c> v1.5.5 :21-31), so a factor on it would do
/// nothing and the term is a count, added after the base's own terms (<c>ApplyFlat</c> divides out whatever
/// factors the perks put on the number).
///
/// The only caller is <c>MobilePartyTrainingBehavior.OnDailyTickParty</c> (v1.5.5 :75): once a day for each non-hero
/// stack of every mobile party, adding <c>Round(ResultNumber * stack size)</c> to the stack's experience, so +5 is 5 per
/// troop and a stack of 40 gets 200. It passes the party and the roster element, never null. The class is the troop's
/// own (<c>TroopClassMapper.FromCharacter</c>, the rule the damage boundary and the counters share), so an infantry pip
/// trains only the party's infantry. Keyed on the party's own LeaderHero, like wages and food.
/// </summary>
public class TaomPartyTrainingModel : DefaultPartyTrainingModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyTrainingModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetEffectiveDailyExperience(MobileParty mobileParty, TroopRosterElement troop)
    {
        var result = base.GetEffectiveDailyExperience(mobileParty, troop);
        _careerPassives.ApplyFlat(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.TroopTraining, TroopClassMapper.FromCharacter(troop.Character));
        return result;
    }
}
