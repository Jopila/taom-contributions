using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.CoopInterop;
using TAOM.Features.TaomBuildings.Hooks;
using TAOM.Features.TaomBuildings.Models;

namespace TAOM.Features.TaomBuildings;

/// <summary>
/// The shared pipeline for buildings TAOM adds to towns and castles (docs/features/taom-buildings.md). A feature
/// contributes a <see cref="TaomBuildingDefinition"/> to the container; this module registers the building types with
/// the campaign, seeds them into loaded saves, decides which variant a new-game town gets, and gives the town screen the
/// sprite and effect line (Patch108).
///
/// <see cref="OwnsSaveData"/> is true although no behavior writes SyncData: a save holds buildings of these types, and
/// the engine drops (<c>Town.AfterLoad</c>) any building whose type is not ready, together with the town's whole build
/// queue. So a failure to register must not pass silently, which is what a fail-closed module does at campaign start:
/// the runner throws out of campaign start. What the player then sees is UNVERIFIED: Patch37's finalizer on
/// <c>Module.OnApplicationTick</c> may swallow the throw and the loading step retry (decision 48,
/// plans/_audit/2026-09-23-opus/DECISIONS.md).
///
/// The registered variant ids are saved in player saves: add new ids, never rename or remove one, and never park this
/// module (a parked module stops registering the types, and Town.AfterLoad strips the buildings).
/// </summary>
internal sealed class TaomBuildingsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch108_TaomBuildings";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new TaomBuildingSeedingBehavior(
            r.Resolve<TaomBuildingSeedingService>(), r.Resolve<ICoopSessionProvider>())),
    };

    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<BuildingModel, TaomBuildingModel>(ModelTarget.Campaign,
            r => new TaomBuildingModel(r.Resolve<TaomBuildingCatalog>())),
    };

    public override string Id => "TaomBuildings";

    // Saved variant ids make this a save-owning module: never park it (see the class summary).
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<TaomBuildingCatalog>(Reuse.Singleton);
        registrator.RegisterMapping<ITaomBuildingUiHook, TaomBuildingCatalog>();
        registrator.Register<IBuildingTypeRegistrarAdapter, BuildingTypeRegistrarAdapter>(Reuse.Singleton);
        registrator.Register<ITownBuildingsAdapter, TownBuildingsAdapter>(Reuse.Singleton);
        registrator.Register<TaomBuildingSeedingService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        TaomBuildingUiHooks.Initialize(() => resolver.Resolve<ITaomBuildingUiHook>());

    public override void RegisterCampaignObjects(IResolver resolver) =>
        resolver.Resolve<IBuildingTypeRegistrarAdapter>().RegisterAll(resolver.Resolve<TaomBuildingCatalog>().Definitions);

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
