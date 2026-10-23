using DryIoc;
using TAOM.Composition;
using TAOM.Core.Domain;

namespace TAOM.Features.UnderTheEaves;

/// <summary>
/// Under the Eaves (#849, docs/features/under-the-eaves.md): forest cuts the main party's sight by registration alone. The
/// contributor reaches <c>TaomMapVisibilityModel</c> because SubModule builds that model with every registered
/// <see cref="IPartySpottingContributor"/>. No behavior, patch, model, mission behavior or save data, so a fault in the
/// registration need not fail closed. It reads the terrain through <c>IMapTerrainAdapter</c>, which Realm Borders registers: the
/// runner registers every module, parked ones included, before anything resolves, so the dependency holds (pinned by
/// UnderTheEavesWiringTests). Order-free: contributors add into one sum and the composer floors the penalties.
/// </summary>
internal sealed class UnderTheEavesModule : TaomFeatureModule
{
    public override string Id => "UnderTheEaves";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IUnderTheEavesSettings, UnderTheEavesSettingsProvider>(Reuse.Singleton);
        registrator.Register<IPartySpottingContributor, ForestSightContributor>(Reuse.Singleton);
    }
}
