using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.ManyHammers;

namespace TAOM.Adapters;

/// <summary>
/// One smelt of a weapon stack through the engine's own crafting behavior (<c>DoSmelting</c>), the instance the smelting
/// screen itself holds. Every number comes from the smithing model installed in the campaign: the energy cost, and the
/// output with its negative charcoal slot, so a cost feat or a cap on the output reaches every unit.
/// </summary>
public sealed class SmeltBenchUnit : ICraftBenchUnit
{
    private readonly Hero _hero;
    private readonly EquipmentElement _weapon;
    private readonly ICraftingCampaignBehavior _behavior;

    public SmeltBenchUnit(Hero hero, EquipmentElement weapon, ICraftingCampaignBehavior behavior)
    {
        _hero = hero ?? throw new ArgumentNullException(nameof(hero));
        _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        _weapon = weapon;
    }

    public UnitSnapshot Read()
    {
        var model = Campaign.Current.Models.SmithingModel;
        var item = _weapon.Item;
        var roster = MobileParty.MainParty.ItemRoster;
        var index = roster.FindIndexOfElement(_weapon);
        return new UnitSnapshot(
            _behavior.GetHeroCraftingStamina(_hero),
            model.GetEnergyCostForSmelting(item, _hero),
            model.GetSmeltingOutputForItem(item),
            CraftBenchStock.Read(model),
            index < 0 ? 0 : roster.GetElementNumber(index));
    }

    public void DoOne() => _behavior.DoSmelting(_hero, _weapon);
}
