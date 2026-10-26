using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TAOM.Features.ManyHammers;

namespace TAOM.Adapters;

/// <summary>
/// One run of a refining formula through the engine's own crafting behavior (<c>DoRefinement</c>). A formula has no stack to
/// run out of, so its supply is unbounded and the materials it takes decide how many fit.
/// </summary>
public sealed class RefineBenchUnit : ICraftBenchUnit
{
    private readonly Hero _hero;
    private readonly Crafting.RefiningFormula _formula;
    private readonly ICraftingCampaignBehavior _behavior;

    public RefineBenchUnit(Hero hero, Crafting.RefiningFormula formula, ICraftingCampaignBehavior behavior)
    {
        _hero = hero ?? throw new ArgumentNullException(nameof(hero));
        _formula = formula ?? throw new ArgumentNullException(nameof(formula));
        _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
    }

    public UnitSnapshot Read()
    {
        var model = Campaign.Current.Models.SmithingModel;
        var formula = _formula;
        return new UnitSnapshot(
            _behavior.GetHeroCraftingStamina(_hero),
            model.GetEnergyCostForRefining(ref formula, _hero),
            NetEffect(_formula),
            CraftBenchStock.Read(model),
            int.MaxValue);
    }

    public void DoOne() => _behavior.DoRefinement(_hero, _formula);

    /// <summary>
    /// What one run does to each material slot: inputs take, outputs give, and an output that is also an input nets against
    /// it. The engine applies only a positive count (<c>DoRefinement</c>), so a zero or negative one touches nothing. No
    /// formula the game or TAOM defines names a material as both input and output; for one that did, the net would be
    /// more generous than the smithy's own per-slot affordability check.
    /// </summary>
    internal static int[] NetEffect(Crafting.RefiningFormula formula)
    {
        var net = new int[UnitSnapshot.Slots];
        Add(net, formula.Input1, -Math.Max(0, formula.Input1Count));
        Add(net, formula.Input2, -Math.Max(0, formula.Input2Count));
        Add(net, formula.Output, Math.Max(0, formula.OutputCount));
        Add(net, formula.Output2, Math.Max(0, formula.Output2Count));
        return net;
    }

    private static void Add(int[] net, CraftingMaterials material, int amount)
    {
        var slot = (int)material;
        if (slot >= 0 && slot < net.Length) net[slot] += amount;
    }
}
