using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.BattleBalance.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.BattleBalance.Models;

public class TaomPartyHealingModel : DefaultPartyHealingModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IBattleBalanceConfigProvider _configProvider;
    private readonly ICareerPassiveService _careerPassives;
    private readonly TAOM.Features.Enlistment.IEnlistmentStateQuery _enlistment;
    private readonly TAOM.Features.FieldCamp.Activities.CampRestHealing _campRest;

    public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
        IBattleBalanceConfigProvider configProvider,
        ICareerPassiveService careerPassives,
        TAOM.Features.Enlistment.IEnlistmentStateQuery enlistment, TAOM.Features.FieldCamp.Activities.CampRestHealing campRest)
    {
        _campRest = campRest;
        _settings = settings;
        _configProvider = configProvider;
        _careerPassives = careerPassives;
        _enlistment = enlistment;
    }

    // The enlisted-player redirect has to run before vanilla's roll, the cultural and career adjustments after it.
    public override float GetSurvivalChance(PartyBase party, CharacterObject character,
        DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
    {
        party = SurvivalChanceLine.RedirectEnlistedPlayer(_enlistment, party, character);
        return SurvivalChanceLine.Apply(_settings, _configProvider, _careerPassives, party,
            base.GetSurvivalChance(party, character, damageType, canDamageKillEvenIfBlunt, enemyParty));
    }

    public override ExplainedNumber GetDailyHealingForRegulars(PartyBase party, bool isPrisoners, bool includeDescriptions = false) =>
        TAOM.Features.FieldCamp.Activities.Hooks.CampRestModelLine.Apply(_campRest, party, isPrisoners, false, base.GetDailyHealingForRegulars(party, isPrisoners, includeDescriptions));

    public override ExplainedNumber GetDailyHealingHpForHeroes(PartyBase party, bool isPrisoners, bool includeDescriptions = false)
    {
        var result = TAOM.Features.FieldCamp.Activities.Hooks.CampRestModelLine.Apply(_campRest, party, isPrisoners, true, base.GetDailyHealingHpForHeroes(party, isPrisoners, includeDescriptions));
        // Career passive: HeroHealing boosts the party's hero daily HP recovery. Resolved
        // via CareerPassiveHero, never party.Owner — the engine getter throws for ownerless
        // settlement parties and this override runs on every settlement daily tick (crash 0b462fd8).
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(party), ref result, PassiveEffectType.HeroHealing);
        return result;
    }
}
