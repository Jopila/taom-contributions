using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.BondsOfBattle.Models;

/// <summary>
/// Barter overpay buys relation up to the Bonds of Battle cap. The loop buys at most 3 points in vanilla (the setting here),
/// before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change.
/// The loop is <see cref="OverpayRelationCalculator"/>. The model reads the hero's relation and the Tribute perk at this
/// boundary and hands plain numbers to the service; the service answers null, and vanilla answers instead, when the
/// feature is off or the player is enlisted. Only the overpay method is overridden, so every other barter price is vanilla.
/// </summary>
public class TaomBarterModel : DefaultBarterModel
{
    private readonly BarterOverpayService _service;

    public TaomBarterModel(BarterOverpayService service)
    {
        _service = service;
    }

    public override int CalculateOverpayRelationIncreaseCosts(Hero hero, float overpayAmount) =>
        (hero == null ? null : _service.CalculateRelationIncrease((int)hero.GetRelationWithPlayer(), overpayAmount, TributeBonus()))
        ?? base.CalculateOverpayRelationIncreaseCosts(hero, overpayAmount);

    // The player hero is read the safe way: the campaign statics throw with no campaign (adapters.md).
    private static float TributeBonus()
    {
        var player = CharacterObject.PlayerCharacter?.HeroObject;
        return player != null && player.GetPerkValue(DefaultPerks.Charm.Tribute) ? DefaultPerks.Charm.Tribute.PrimaryBonus : 0f;
    }
}
