using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.HarvestRoad;
using TAOM.Features.HarvestRoad.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HarvestRoad;

/// <summary>Wiring guard for Harvest Road (#912): drop the module line and the notices go quiet, with every other test still green.</summary>
[TestClass]
public class HarvestRoadWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<HarvestRoadModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedHarvestRoad_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new HarvestRoadModule();

        Assert.AreEqual("HarvestRoad", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesItsPatchesAtGameInit_AndOnlyThat()
    {
        var decls = new HarvestRoadModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch120_HarvestRoad", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModelAndNoMissionBehavior()
    {
        var module = new HarvestRoadModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void RegisterServices_MakesTheServiceResolvable_AsASingleton()
    {
        using var c = new Container();
        var module = new HarvestRoadModule();
        module.RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());

        Assert.AreSame(c.Resolve<HarvestRoadService>(), c.Resolve<HarvestRoadService>());
        Assert.IsInstanceOfType(c.Resolve<IHarvestRoadSettingsProvider>(), typeof(HarvestRoadSettingsProvider));
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToThePatches_AndCanRunTwice()
    {
        using var c = new Container();
        var module = new HarvestRoadModule();
        module.RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());

        module.InitializeStatics(c);
        module.InitializeStatics(c);

        Assert.AreSame(c.Resolve<HarvestRoadService>(), HarvestRoadPatches.Service);
    }

    [TestMethod]
    public void TheCategory_IsNotRegisteredByHandInIoC()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch120") || src.Contains("HarvestRoad"), "Main/IoC.cs must not name the module");
    }

    [TestMethod]
    public void TheCategory_IsNotAppliedByHandInSubModule()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch120") || src.Contains("HarvestRoad"), "Main/SubModule.cs must not name the module");
    }
}
