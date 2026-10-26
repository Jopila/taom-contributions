using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ICraftingWorkshopAdapter"/> over one crafting hero (#910). The items come from the
/// smithing model's own material lookup, so a model that swaps a grade is followed, and the tools good is the engine's
/// <c>DefaultItems.Tools</c>: no item id is spelled here. The stamina is read and written through the crafting behavior's public
/// interface, and the cost is the model's refining cost for a formula of one iron and one hardwood. A formula's output is a
/// <c>CraftingMaterials</c> value and Tools is not one, so the formula names no output (count 0); the model's refining cost reads
/// the formula's inputs at most.
/// </summary>
public sealed class CraftingWorkshopAdapter : ICraftingWorkshopAdapter
{
    private const CraftingMaterials IronGrade = CraftingMaterials.Iron3;
    private const CraftingMaterials Hardwood = CraftingMaterials.Wood;

    private readonly Hero _hero;
    private readonly ICraftingCampaignBehavior _crafting;
    private readonly SmithingModel _model;

    private CraftingWorkshopAdapter(Hero hero, ICraftingCampaignBehavior crafting, SmithingModel model)
    {
        _hero = hero;
        _crafting = crafting;
        _model = model;
    }

    /// <summary>
    /// The adapter for a hero, or null where Make Tools cannot work: no hero, no live campaign, a game mode other than the
    /// campaign (the tutorial opens the same screen with no crafting record), or no crafting behavior.
    /// </summary>
    public static CraftingWorkshopAdapter? For(Hero? hero)
    {
        var campaign = Campaign.Current;
        if (hero == null || campaign == null || campaign.GameMode != CampaignGameMode.Campaign)
            return null;
        var behavior = campaign.GetCampaignBehavior<ICraftingCampaignBehavior>();
        var model = campaign.Models?.SmithingModel;
        return behavior == null || model == null ? null : new CraftingWorkshopAdapter(hero, behavior, model);
    }

    private ItemObject IronItem => _model.GetCraftingMaterialItem(IronGrade);

    private ItemObject WoodItem => _model.GetCraftingMaterialItem(Hardwood);

    private static ItemRoster Stores => MobileParty.MainParty.ItemRoster;

    public int IronCount => Stores.GetItemNumber(IronItem);

    public int WoodCount => Stores.GetItemNumber(WoodItem);

    public int Stamina => _crafting.GetHeroCraftingStamina(_hero);

    public int RefiningCost
    {
        get
        {
            var formula = new Crafting.RefiningFormula(IronGrade, 1, Hardwood, 1, CraftingMaterials.IronOre, 0);
            return _model.GetEnergyCostForRefining(ref formula, _hero);
        }
    }

    public int ToolsValue => DefaultItems.Tools.Value;

    public int IronValue => IronItem.Value;

    public int WoodValue => WoodItem.Value;

    public void TakeInputs()
    {
        Stores.AddToCounts(IronItem, -1);
        Stores.AddToCounts(WoodItem, -1);
    }

    public void GiveTools() => Stores.AddToCounts(DefaultItems.Tools, 1);

    public void SetStamina(int value) => _crafting.SetHeroCraftingStamina(_hero, value);

    public void AddCraftingXp(float xp) => _hero.AddSkillXp(DefaultSkills.Crafting, xp);
}
