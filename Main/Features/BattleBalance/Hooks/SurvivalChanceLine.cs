using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CulturalFeats;
using TAOM.Features.Enlistment;

namespace TAOM.Features.BattleBalance.Hooks;

/// <summary>
/// The healing model's boundary for the survival roll: the enlisted-player party redirect (before vanilla's roll), then the
/// cultural survival bonus and the career TroopSurvival passive (after it). Engine facts are read here and the arithmetic is
/// <see cref="SurvivalChanceMath"/>, so the model makes two calls around base: the redirect before it, Apply after.
/// </summary>
internal static class SurvivalChanceLine
{
    /// <summary>
    /// Substitutes the commander's party when the PLAYER's own survival is being rolled while enlisted. Vanilla reads the
    /// surgeon bonus and CheatDeath off the passed party's effective surgeon, which falls back to the party leader, so an
    /// enlisted player's own party (one parked, hidden hero) rolls him with only his own Medicine and perks. The redirect rolls
    /// him against his commander's surgeon instead: better when his own Medicine is lower, worse when his Medicine or
    /// CheatDeath is better. PhysicianOfPeople skips heroes. Redirected here rather than by a Harmony patch because TAOM
    /// already owns the model.
    ///
    /// Narrow on purpose: the character is the player, the party is the player's own, service is live and the commander
    /// resolves to a real party. Any failure returns the original party untouched. Never throws: a survival roll must not.
    /// </summary>
    public static PartyBase RedirectEnlistedPlayer(IEnlistmentStateQuery enlistment, PartyBase party, CharacterObject character)
    {
        try
        {
            if (party == null || character == null || !character.IsPlayerCharacter)
                return party;
            if (enlistment?.IsEnlisted != true)
                return party;
            if (party != PartyBase.MainParty)
                return party;

            var commanderId = enlistment.CommanderHeroId;
            if (string.IsNullOrEmpty(commanderId))
                return party;

            var commanderParty = Campaign.Current?.CampaignObjectManager
                ?.Find<Hero>(commanderId)?.PartyBelongedTo?.Party;

            // Never return null: vanilla rolls a non-prisoner with no mobile party at 0% survival.
            return commanderParty ?? party;
        }
        catch
        {
            return party;
        }
    }

    /// <summary>Applies the cultural bonus, then the career passive, to vanilla's survival chance for the (redirected) party.</summary>
    public static float Apply(IBattleBalanceSettingsProvider settings, IBattleBalanceConfigProvider configProvider,
        ICareerPassiveService careerPassives, PartyBase party, float vanillaSurvival)
    {
        if (party == null)
            return vanillaSurvival;

        float result = vanillaSurvival;

        if (settings.EnableCulturalSurvivalBonuses)
        {
            var config = configProvider.GetConfig();
            if (config.CasualtyRatios.EnableCulturalSurvivalBonuses)
            {
                // Vanilla PartyBaseHelper.HasFeat precedence: the leader, party, owner, settlement walk.
                var culture = CultureFeatAdapter.ResolvePartyCulture(party);
                if (culture != null)
                    result = SurvivalChanceMath.ApplyCulturalSurvivalBonus(
                        result, config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId));
            }
        }

        var heroId = CareerPassiveHero.ResolveId(party);
        if (heroId == null)
            return result;
        return SurvivalChanceMath.ApplyCareerSurvival(
            result, careerPassives.GetPassiveMagnitude(heroId, PassiveEffectType.TroopSurvival));
    }
}
