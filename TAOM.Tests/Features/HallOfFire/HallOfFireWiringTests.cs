using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.HallOfFire;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// Wiring regression guard for the conversation backdrop fallback (#826). Drop the module line and every TAOM
/// settlement conversation asks the engine for a missing atmosphere again; drop the MainMenu install and the
/// wrapper is never put in the engine slot. Methods that touch engine types carry <c>RequiresGame</c>.
/// </summary>
[TestClass]
public class HallOfFireWiringTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void FeatureModules_ListTheHallOfFireModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<HallOfFireModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedHallOfFire_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new HallOfFireModule();

        Assert.AreEqual("HallOfFire", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new HallOfFireModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
    }

    private static Container BuildContainer(string tempDir, out IMapConversationProviderSlot slot, out IMapConversationDataProvider vanilla)
    {
        var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(tempDir);
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());

        new HallOfFireModule().RegisterServices(container);

        slot = Substitute.For<IMapConversationProviderSlot>();
        vanilla = Substitute.For<IMapConversationDataProvider>();
        slot.Current.Returns(vanilla);
        container.RegisterInstance(slot, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        return container;
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_RegistersTheServicesAsSingletons()
    {
        using var container = BuildContainer(Path.GetTempPath(), out _, out _);

        Assert.AreSame(container.Resolve<HallOfFireService>(), container.Resolve<HallOfFireService>());
        Assert.AreSame(container.Resolve<IHallOfFireConfigProvider>(), container.Resolve<IHallOfFireConfigProvider>());
        Assert.IsInstanceOfType(container.Resolve<IHallOfFireConfigProvider>(), typeof(HallOfFireConfigProvider));
        Assert.AreSame(container.Resolve<HallOfFireInstaller>(), container.Resolve<HallOfFireInstaller>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_RegistersTheEngineSlotAdapter()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());

        new HallOfFireModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<IMapConversationProviderSlot>(), typeof(MapConversationProviderSlot));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnPhase_MainMenu_WrapsTheCurrentProvider()
    {
        using var container = BuildContainer(Path.GetTempPath(), out var slot, out var vanilla);

        new HallOfFireModule().OnPhase(ApplyPhase.MainMenu, container);

        slot.Received(1).Set(NSubstitute.Arg.Is<IMapConversationDataProvider>(p =>
            p is TaomMapConversationDataProvider && ((TaomMapConversationDataProvider)p).Inner == vanilla));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnPhase_OtherPhases_InstallNothing()
    {
        using var container = BuildContainer(Path.GetTempPath(), out var slot, out _);

        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.GameInit, ApplyPhase.FirstMission })
            new HallOfFireModule().OnPhase(phase, container);

        slot.DidNotReceive().Set(NSubstitute.Arg.Any<IMapConversationDataProvider>());
    }
}
