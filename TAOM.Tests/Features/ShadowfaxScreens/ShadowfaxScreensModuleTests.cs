using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Tests.Features.ShadowfaxScreens;

[TestClass]
public class ShadowfaxScreensModuleTests
{
    private Container _container = null!;
    private readonly ShadowfaxScreensModule _sut = new ShadowfaxScreensModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        // IStampClock and IModLogger belong to other registrations; the module must not add them.
        _container.RegisterInstance(Substitute.For<IStampClock>());
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ScreenOpenTiming.Initialize(null);
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_ProviderAndTimerResolveAsSingletons()
    {
        var provider = _container.Resolve<IShadowfaxScreensSettingsProvider>();
        var timer = _container.Resolve<ScreenOpenTimer>();

        Assert.IsInstanceOfType(provider, typeof(ShadowfaxScreensSettingsProvider));
        Assert.AreSame(provider, _container.Resolve<IShadowfaxScreensSettingsProvider>());
        Assert.AreSame(timer, _container.Resolve<ScreenOpenTimer>());
    }

    [TestMethod]
    public void RegisterServices_DoesNotRegisterTheStampClock()
    {
        var fresh = new Container();
        new ShadowfaxScreensModule().RegisterServices(fresh);

        Assert.IsFalse(fresh.IsRegistered<IStampClock>());
        fresh.Dispose();
    }

    [TestMethod]
    public void InitializeStatics_WiresTheFacadeToTheTimer()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<ScreenOpenTimer>(), ScreenOpenTiming.Timer);
    }

    [TestMethod]
    public void Module_IsListedOnce_AndDeclaresItsCategoriesAtGameInit()
    {
        var modules = FeatureModules.All.OfType<ShadowfaxScreensModule>().ToList();

        Assert.AreEqual(1, modules.Count, "ShadowfaxScreensModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("ShadowfaxScreens", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        CollectionAssert.AreEquivalent(
            new[] { "PatchXXX_ShadowfaxClan", "PatchXXX_ShadowfaxInventory", "PatchXXX_ShadowfaxParty", "PatchXXX_ShadowfaxQuests" },
            modules[0].PatchCategories.Select(d => d.Category).ToList());
        Assert.IsTrue(modules[0].PatchCategories.All(d => d.Phase == ApplyPhase.GameInit));
    }
}
