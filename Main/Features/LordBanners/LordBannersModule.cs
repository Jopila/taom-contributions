using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Composition;
using TAOM.Features.LordBanners.Models;

namespace TAOM.Features.LordBanners;

/// <summary>
/// Lord Banners (#823, docs/features/lord-banners.md): lets lords of TAOM cultures receive banner items, through a
/// campaign <c>BannerItemModel</c>, and gives a joining vassal a banner through a campaign <c>VassalRewardsModel</c>. No patch, behavior or save data.
/// </summary>
internal sealed class LordBannersModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<BannerItemModel, TaomBannerItemModel>(ModelTarget.Campaign,
            resolver => new TaomBannerItemModel(resolver.Resolve<LordBannerService>())),
        GameModelDecl.Of<VassalRewardsModel, TaomVassalRewardsModel>(ModelTarget.Campaign,
            resolver => new TaomVassalRewardsModel(resolver.Resolve<LordBannerService>())),
    };

    public override string Id => "LordBanners";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ILordBannerCulturesConfigProvider, LordBannerCulturesConfigProvider>(Reuse.Singleton);
        registrator.Register<LordBannerService>(Reuse.Singleton);
    }

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
