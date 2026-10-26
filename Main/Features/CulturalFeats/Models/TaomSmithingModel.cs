using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.SmithsMeasure;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSmithingModel : DefaultSmithingModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly ISmithsMeasureSettingsProvider _smithsMeasure;

    // Resolved here, not passed in: a constructor parameter would need an edit to SubModule.cs, where this model is built
    // (TaomPartySizeModel does the same for its patrol hook). The provider reads its settings live, so one resolve serves the campaign.
    public TaomSmithingModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
        : this(feats, careerPassives, IoC.Resolve<ISmithsMeasureSettingsProvider>())
    {
    }

    // The seam for TAOM.Tests.
    internal TaomSmithingModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives, ISmithsMeasureSettingsProvider smithsMeasure)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _smithsMeasure = smithsMeasure;
    }

    // Smith's Measure (#910): the engine's output, with its own reductions and the charcoal cost already applied, then the cap.
    // The smelting list, its preview and the smelt itself all ask this method.
    public override int[] GetSmeltingOutputForItem(ItemObject item)
        => CapSmelting(base.GetSmeltingOutputForItem(item));

    internal int[] CapSmelting(int[] output)
        => _smithsMeasure.CapSmelting ? SmeltingYieldCap.Apply(output, _smithsMeasure.YieldCap) : output;

    public override int GetEnergyCostForSmithing(ItemObject item, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForSmithing(item, hero), hero);

    public override int GetEnergyCostForSmelting(ItemObject item, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForSmelting(item, hero), hero);

    public override int GetEnergyCostForRefining(ref Crafting.RefiningFormula refineFormula, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForRefining(ref refineFormula, hero), hero);

    /// <summary>
    /// Boundary helper — single shared <see cref="ExplainedNumber"/> + cast composes
    /// the culture feat factor and the career passive factor without the double
    /// truncation bug fixed in Phase 9b #173 F4.
    /// </summary>
    private int ApplyFeatReduction(int baseCost, Hero hero)
    {
        var result = new ExplainedNumber(baseCost, false);
        _feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
        _careerPassives.ApplyFactor(hero?.StringId, ref result, PassiveEffectType.SmithingCostReduction);
        return (int)result.ResultNumber;
    }
}
