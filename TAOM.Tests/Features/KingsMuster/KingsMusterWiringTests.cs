using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.KingsMuster;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.KingsMuster;

/// <summary>Wiring guard for the King's Muster (#874): one module line, a service the army-management model resolves once, and nothing in the single-owner files.</summary>
[TestClass]
public class KingsMusterWiringTests
{
    private const string ModelPath = "Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs";

    [TestMethod]
    public void FeatureModules_ListTheKingsMusterModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<KingsMusterModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedKingsMuster_IsNotParked_OwnsNoSaveData_AndDeclaresNoModelPatchOrBehavior()
    {
        var module = new KingsMusterModule();

        Assert.AreEqual("KingsMuster", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.GameModels.Count, "the army-management slot is already filled by TaomArmyManagementModel; this module declares no second model");
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_RegistersTheSettingsProviderAndTheService_AndTheServiceResolvesFromARealContainer()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        // The factor lives in army_targeting.json, which ArmyTargetingIoC registers in the real game.
        container.Register<IArmyTargetingConfigProvider, ArmyTargetingConfigProvider>(Reuse.Singleton);
        new KingsMusterModule().RegisterServices(container);

        var service = container.Resolve<KingsMusterService>();

        Assert.AreSame(service, container.Resolve<KingsMusterService>(), "one shared instance");
        Assert.IsInstanceOfType(container.Resolve<IKingsMusterSettingsProvider>(), typeof(KingsMusterSettingsProvider));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomArmyManagementModel_DeclaresOnlyItsTwoTrunkOverrides_AndKeepsItsConstructor()
    {
        var overridden = typeof(TaomArmyManagementModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(new[] { "CalculatePartyInfluenceCost", "DailyBeingAtArmyInfluenceAward" }, overridden);
        Assert.AreEqual(typeof(DefaultArmyManagementCalculationModel), typeof(TaomArmyManagementModel).BaseType);
        CollectionAssert.AreEqual(
            new[] { typeof(ICulturalFeatsService) },
            typeof(TaomArmyManagementModel).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToArray(),
            "SubModule.cs builds the model with `new`: a new parameter would force an edit to that single-owner file");
    }

    [TestMethod]
    public void TheModel_ResolvesTheServiceOnceInItsConstructor()
    {
        var src = RepoPaths.ReadSource(ModelPath, stripComments: true);

        StringAssert.Contains(src, "private readonly KingsMusterService");
        Assert.AreEqual(1, Regex.Matches(src, @"IoC\.Resolve<KingsMusterService>\(\)").Count);
        var ctor = src.IndexOf("public TaomArmyManagementModel(", StringComparison.Ordinal);
        var firstMethod = src.IndexOf("DailyBeingAtArmyInfluenceAward(", ctor, StringComparison.Ordinal);
        var resolve = src.IndexOf("IoC.Resolve<KingsMusterService>", StringComparison.Ordinal);
        Assert.IsTrue(ctor >= 0 && ctor < resolve && resolve < firstMethod, "the service must be resolved inside the constructor");
    }

    [TestMethod]
    public void TheDiscount_FollowsTheCultureFeats()
    {
        var src = RepoPaths.ReadSource(ModelPath, stripComments: true);

        var cost = src.IndexOf("CalculatePartyInfluenceCost(", StringComparison.Ordinal);
        var discount = src.IndexOf("ApplyRulerDiscount(", cost, StringComparison.Ordinal);
        var feats = src.IndexOf("_feats.ApplyArmyInfluenceCost(", cost, StringComparison.Ordinal);
        Assert.IsTrue(cost >= 0 && feats > cost && discount > cost, "CalculatePartyInfluenceCost must call both");
        Assert.IsTrue(discount < feats, "the discount wraps the feats' result: it is the outer call, so it applies after them");
    }

    [TestMethod]
    public void TheModelBodies_HoldNoBranching()
    {
        var src = RepoPaths.ReadSource(ModelPath, stripComments: true);
        var bodies = src.Substring(src.IndexOf("public TaomArmyManagementModel(", StringComparison.Ordinal));

        Assert.IsFalse(Regex.IsMatch(bodies, @"\b(if|foreach|for|while|switch)\b|\?|&&|\|\|"), "gamemodels.md rule 4: delegate, never branch");
    }

    [DataTestMethod]
    [DataRow("Main/IoC.cs")]
    [DataRow("Main/SubModule.cs")]
    public void TheSingleOwnerFiles_NameNothingOfTheFeature(string path)
    {
        var text = RepoPaths.ReadSource(path);

        Assert.IsFalse(text.Contains("KingsMuster"), path + " is single-owner: the module registers through FeatureModules.cs only");
    }
}
