using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Features.FieldCamp;
using TAOM.Features.RealmBorders;
using TAOM.Features.UnderTheEaves;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.UnderTheEaves;

/// <summary>
/// Wiring guard for Under the Eaves (#849): one line in FeatureModules, no IoC.cs or SubModule.cs mention, the module declares
/// registration and nothing else (no patch, behavior, model, mission behavior or save data), and the contributor reaches the
/// sight model the way the lookout and Far Watch do, by being registered as an <see cref="IPartySpottingContributor"/>.
/// </summary>
[TestClass]
public class UnderTheEavesWiringTests
{
    private static Container NewContainer()
    {
        var container = new Container();
        new RealmBordersModule().RegisterServices(container);
        new UnderTheEavesModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<UnderTheEavesModule>().Count(),
            "UnderTheEavesModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void TheModule_IsNamedUnderTheEaves_NotParked_OwnsNoSaveData_AndDeclaresNothingButServices()
    {
        var module = new UnderTheEavesModule();

        Assert.AreEqual("UnderTheEaves", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "stateless: nothing is saved, no key is written");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the rule rides the contributor seam");
        Assert.AreEqual(0, module.GameModels.Count, "no second MapVisibilityModel: the career model owns the slot");
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void IoC_AndSubModule_DoNotMentionTheFeature()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("UnderTheEaves") || src.Contains("ForestSight"),
            "the module registers everything; a hand registration doubles it");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheContributor_ResolvesFromARealContainer_AsAnIPartySpottingContributor_BesideTheLookout()
    {
        using var container = NewContainer();
        container.Register<IPartySpottingContributor, LookoutSpottingContributor>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<ICampSettingsProvider>());
        container.RegisterInstance(Substitute.For<ICampService>());

        var all = container.ResolveMany<IPartySpottingContributor>().ToList();

        Assert.AreEqual(1, all.OfType<ForestSightContributor>().Count(), "a Register appends (the lookout precedent), so it reaches the model");
        Assert.AreEqual(1, all.OfType<LookoutSpottingContributor>().Count());
        Assert.IsInstanceOfType(container.Resolve<IUnderTheEavesSettings>(), typeof(UnderTheEavesSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IMapTerrainAdapter>(), typeof(MapTerrainAdapter),
            "the terrain adapter comes from Realm Borders' registration; the contributor needs it at resolve time");
    }

    [TestMethod]
    public void TheModule_RegistersExactlyOneContributor_AndTheProviderAsASingleton()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IMapTerrainAdapter>());
        new UnderTheEavesModule().RegisterServices(container);

        Assert.AreEqual(1, container.ResolveMany<IPartySpottingContributor>().Count());
        Assert.AreSame(container.Resolve<IUnderTheEavesSettings>(), container.Resolve<IUnderTheEavesSettings>());
        Assert.AreSame(container.ResolveMany<IPartySpottingContributor>().Single(), container.ResolveMany<IPartySpottingContributor>().Single(),
            "a singleton: the model holds the same instance a later resolve returns");
    }

    [TestMethod]
    public void TheFeaturesFiles_AreSmallEntryPoints_AndHoldNoServiceLocator()
    {
        var files = System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "UnderTheEaves"), "*.cs");

        Assert.IsTrue(files.Length >= 5);
        foreach (var file in files)
        {
            Assert.IsTrue(System.IO.File.ReadAllLines(file).Length < 150, "ADR-002: " + System.IO.Path.GetFileName(file));
            Assert.IsFalse(RepoPaths.MentionsOutsideComments(file, "IoC.Resolve"), System.IO.Path.GetFileName(file) + " resolves through the locator");
        }
    }
}
