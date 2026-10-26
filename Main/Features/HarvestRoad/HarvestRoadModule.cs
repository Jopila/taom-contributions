using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.HarvestRoad.Hooks;

namespace TAOM.Features.HarvestRoad;

/// <summary>
/// Harvest Road (#912, docs/features/harvest-road.md): the small trade pop-ups over a town's nameplate, which vanilla
/// raises only for caravans, also appear when villagers deliver goods and when AI parties buy food. A pure roster diff and
/// a service for the gates; two patches (Patch120, applied at GameInit) take the roster before and after the vanilla trade
/// and raise the vanilla event. No behavior, no model, no save data, nothing in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class HarvestRoadModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch120_HarvestRoad";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "HarvestRoad";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHarvestRoadSettingsProvider, HarvestRoadSettingsProvider>(Reuse.Singleton);
        registrator.Register<HarvestRoadService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        HarvestRoadPatches.Initialize(resolver.Resolve<HarvestRoadService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
