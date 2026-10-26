using System.IO;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.SmithsMeasure;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// Wiring guards for Smith's Measure (#910). Each piece fails silently when unwired: drop the module line and the services are
/// never registered, so the model's constructor throws at campaign start; drop the override and smelting is vanilla.
/// </summary>
[TestClass]
public class SmithsMeasureWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new SmithsMeasureModule().RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheSmithsMeasureModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SmithsMeasureModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.SmithsMeasure.SmithsMeasureModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("SmithsMeasure"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("SmithsMeasure"));
    }

    [TestMethod]
    public void TheSmithingModel_IsStillBuiltByTheSubModuleWithItsTwoServices()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsTrue(src.Contains("new TaomSmithingModel(culturalFeats, careerPassives)"),
            "the model resolves its own settings, so the single-owner line does not change");
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_PatchesNothing_AndDeclaresNoModelOrBehavior()
    {
        var module = new SmithsMeasureModule();

        Assert.AreEqual("SmithsMeasure", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the roster and the stamina are the engine's own saved state");
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count, "TaomSmithingModel is added by SubModule already");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<ISmithsMeasureSettingsProvider>());
        Assert.IsNotNull(container.Resolve<MakeToolsService>());
    }

    [TestMethod]
    public void TheServices_AreSharedInstances()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<MakeToolsService>(), container.Resolve<MakeToolsService>());
        Assert.AreSame(container.Resolve<ISmithsMeasureSettingsProvider>(), container.Resolve<ISmithsMeasureSettingsProvider>());
    }

    [TestMethod]
    public void SmithsMeasure_DeclaresNoHarmonyPatch()
    {
        var files = new[] { "Main", "Client" }
            .SelectMany(root => Directory.GetFiles(RepoPaths.RepoPath(root, "Features", "SmithsMeasure"), "*.cs", SearchOption.AllDirectories))
            .ToList();

        Assert.IsTrue(files.Count > 0);
        foreach (var file in files)
            Assert.IsFalse(RepoPaths.MentionsOutsideComments(file, "HarmonyPatch"), file + " declares a Harmony patch");
    }

    [TestMethod]
    public void TheSmithingModel_OverridesSmeltingOutput_OnItsOwn()
    {
        var method = typeof(TaomSmithingModel).GetMethod("GetSmeltingOutputForItem",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNotNull(method, "TaomSmithingModel must override GetSmeltingOutputForItem");
        Assert.IsTrue(method!.GetBaseDefinition().DeclaringType != typeof(TaomSmithingModel), "an override, not a new method");
    }

    [TestMethod]
    public void TheSmithingModelOverride_CallsTheBase_ThenTheCap_InOneLine()
    {
        var src = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomSmithingModel.cs", stripComments: true);

        StringAssert.Contains(src, "CapSmelting(base.GetSmeltingOutputForItem(item))");
        StringAssert.Contains(src, "IoC.Resolve<ISmithsMeasureSettingsProvider>()");
    }

    [TestMethod]
    public void TheSmithingModel_ResolvesItsSettingsOnceInTheConstructor_NotPerSmelt()
    {
        var src = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomSmithingModel.cs", stripComments: true);

        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, "IoC\\.Resolve<").Count);
    }
}
