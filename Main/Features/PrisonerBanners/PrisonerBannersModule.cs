using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.PrisonerBanners;

/// <summary>
/// Registers the settings provider for the party screen's prisoner banners (#902); the mixin lives in the Client submodule.
/// </summary>
internal sealed class PrisonerBannersModule : TaomFeatureModule
{
    public override string Id => "PrisonerBanners";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<PrisonerBannersSettingsProvider>(Reuse.Singleton);
    }
}
