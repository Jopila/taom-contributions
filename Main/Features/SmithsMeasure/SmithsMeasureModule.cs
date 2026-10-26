using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.SmithsMeasure;

/// <summary>
/// Smith's Measure (docs/features/smiths-measure.md, #910): a cap on what smelting returns, and a Make Tools button on the
/// refinement tab. This module registers the services only. The cap is a method on <c>TaomSmithingModel</c>, which
/// <c>SubModule</c> already adds, so it needs no model declaration here; the button is a UIExtenderEx mixin and prefab in
/// TAOM.Client (<c>Client/Features/SmithsMeasure/UI</c>), found by that assembly's scan and so listed nowhere. No Harmony patch,
/// no campaign behavior and nothing of TAOM's saved: the roster and the crafting stamina are the engine's own saved state.
/// </summary>
internal sealed class SmithsMeasureModule : TaomFeatureModule
{
    public override string Id => "SmithsMeasure";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISmithsMeasureSettingsProvider, SmithsMeasureSettingsProvider>(Reuse.Singleton);
        registrator.Register<MakeToolsService>(Reuse.Singleton);
    }
}
