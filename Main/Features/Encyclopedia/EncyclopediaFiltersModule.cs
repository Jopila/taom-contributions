using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia.Adapters;
using TAOM.Features.Encyclopedia.Hooks;

namespace TAOM.Features.Encyclopedia;

/// <summary>
/// Race and Age filters on the encyclopedia hero page (#907). One postfix; no model, behavior or save data. Applies at
/// ProcessLoad because SandBox builds the encyclopedia pages in its own OnGameInitializationFinished
/// (SandBoxSubModule.cs:117,125), which runs before TAOM's, so a GameInit patch would miss the first campaign.
/// </summary>
internal sealed class EncyclopediaFiltersModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch119_EncyclopediaHeroFilters";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "EncyclopediaFilters";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroFilterConfigProvider, HeroFilterConfigProvider>(Reuse.Singleton);
        registrator.Register<IEncyclopediaHeroFilterAdapter, EncyclopediaHeroFilterAdapter>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(
            resolver.Resolve<IEncyclopediaHeroFilterAdapter>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
