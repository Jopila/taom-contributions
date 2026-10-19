using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordBanners;
using TAOM.Features.LordBanners.Models;

namespace TAOM.Tests.Features.LordBanners;

/// <summary>
/// Wiring regression guard for lord banners (#823). Drop the module line and TAOM lords never get a banner again;
/// drop the model declaration and the map is never consulted. Methods that touch engine types carry
/// <c>RequiresGame</c>.
/// </summary>
[TestClass]
public class LordBannersWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheLordBannersModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<LordBannersModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedLordBanners_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new LordBannersModule();

        Assert.AreEqual("LordBanners", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoPatchCategory()
    {
        var module = new LordBannersModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultBannerItemModel()
    {
        var decl = new LordBannersModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(BannerItemModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomBannerItemModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultBannerItemModel), decl.ModelType.BaseType);
    }

    [TestMethod]
    public void TheModule_RegistersTheServicesAsSingletons()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());

        new LordBannersModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<LordBannerService>(), container.Resolve<LordBannerService>());
        Assert.AreSame(container.Resolve<ILordBannerCulturesConfigProvider>(), container.Resolve<ILordBannerCulturesConfigProvider>());
        Assert.IsInstanceOfType(container.Resolve<ILordBannerCulturesConfigProvider>(), typeof(LordBannerCulturesConfigProvider));
    }
}
