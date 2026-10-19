using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.PosternGate;
using TAOM.Features.TaomBuildings;
using TAOM.Features.TaomBuildings.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>
/// Wiring guard for the shared building pipeline (#855). Every piece fails silently when unwired: drop the module line and
/// no building type is registered (Town.AfterLoad then strips saved buildings); drop the category and the town screen shows
/// the default icon; drop the model and a new game never places the building. Methods that touch engine types carry
/// <c>RequiresGame</c>.
/// </summary>
[TestClass]
public class TaomBuildingsWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheTaomBuildingsModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<TaomBuildingsModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedTaomBuildings_AndIsNotParked_AndOwnsSaveData()
    {
        var module = new TaomBuildingsModule();

        Assert.AreEqual("TaomBuildings", module.Id);
        Assert.IsNull(module.ParkedReason, "the building types must stay registered or saved buildings are stripped");
        Assert.IsTrue(module.OwnsSaveData,
            "a save holds buildings of these types: if registration fails, Town.AfterLoad drops them and clears the build queue, so the module fails closed");
    }

    [TestMethod]
    public void TheModule_AppliesPatch108AtGameInit_AndOnlyThat()
    {
        var decls = new TaomBuildingsModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch108_TaomBuildings", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultBuildingModel()
    {
        var decl = new TaomBuildingsModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(BuildingModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomBuildingModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultBuildingModel), decl.ModelType.BaseType, "every other building rule stays the engine's");
    }

    [TestMethod]
    public void TheCatalog_HoldsThePosternGateFamily_WithTheConfiguredCosts_AndTheHookIsTheSameSingleton()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        container.RegisterInstance(paths);

        new TaomBuildingsModule().RegisterServices(container);
        new PosternGateModule().RegisterServices(container);

        var catalog = container.Resolve<TaomBuildingCatalog>();
        var family = catalog.Definitions.Single(d => d.FamilyId == "postern_gate");
        CollectionAssert.AreEqual(new[] { 400, 700, 1000 }, family.Costs.ToArray());
        Assert.AreEqual(6, family.Variants.Count);
        Assert.AreSame(catalog, container.Resolve<TaomBuildingCatalog>());
        Assert.AreSame(catalog, container.Resolve<ITaomBuildingUiHook>(), "the patches and the model must share one catalog");
        Assert.IsInstanceOfType(container.Resolve<ITownBuildingsAdapter>(), typeof(TownBuildingsAdapter));
        Assert.IsInstanceOfType(container.Resolve<IBuildingTypeRegistrarAdapter>(), typeof(BuildingTypeRegistrarAdapter));
    }
}
