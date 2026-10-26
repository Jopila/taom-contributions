using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BanditVoices;
using TAOM.Features.BanditVoices.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BanditVoices;

/// <summary>Wiring guard for Bandit Voices (#900): drop the module line and the greetings go quiet, with every other test green.</summary>
[TestClass]
public class BanditVoicesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<BanditVoicesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedBanditVoices_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new BanditVoicesModule();

        Assert.AreEqual("BanditVoices", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch122AtGameInit_AndOnlyThat()
    {
        var decls = new BanditVoicesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch122_BanditVoices", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void ThePatchCategory_IsDeclaredByNoOtherModule()
    {
        var all = FeatureModules.All
            .SelectMany(m => m.PatchCategories)
            .Count(d => d.Category == "Patch122_BanditVoices");

        Assert.AreEqual(1, all);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModelAndNoMissionBehavior()
    {
        var module = new BanditVoicesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    private static Container Registered()
    {
        var c = new Container();
        new BanditVoicesModule().RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());
        c.RegisterInstance(Substitute.For<IRandomSource>());
        c.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        c.RegisterInstance(Substitute.For<IMessengerConversationState>());
        return c;
    }

    [TestMethod]
    public void RegisterServices_MakesTheServiceResolvable_AsASingleton()
    {
        using var c = Registered();

        Assert.AreSame(c.Resolve<BanditVoicesService>(), c.Resolve<BanditVoicesService>());
        Assert.IsInstanceOfType(c.Resolve<IBanditVoicesSettingsProvider>(), typeof(BanditVoicesSettingsProvider));
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToThePatch_AndCanRunTwice()
    {
        using var c = Registered();
        var module = new BanditVoicesModule();

        module.InitializeStatics(c);
        module.InitializeStatics(c);

        Assert.AreSame(c.Resolve<BanditVoicesService>(), BanditGreetingPatch.Service);
    }

    [TestMethod]
    public void TheModule_IsNotRegisteredByHandInIoC()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch122") || src.Contains("BanditVoices"), "Main/IoC.cs must not name the module");
    }

    [TestMethod]
    public void TheCategory_IsNotAppliedByHandInSubModule()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch122") || src.Contains("BanditVoices"), "Main/SubModule.cs must not name the module");
    }
}
