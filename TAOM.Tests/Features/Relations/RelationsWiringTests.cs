using System;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;
using TAOM.Features.Relations.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// Wiring guard for the Relations module. Each piece fails silently when unwired: drop the module line and no seeding, notice
/// patch or personal pair ever runs; resolve the service before the container exists and the model's construction throws
/// inside OnGameStart; register the seam twice and every resolve of it throws.
/// </summary>
[TestClass]
public class RelationsWiringTests
{
    private static Container Container()
    {
        var container = new Container();
        new RelationsModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheRelationsModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<RelationsModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNotParked_AndOwnsSaveData_BecauseTheSeedingFlagIsPersisted()
    {
        var module = new RelationsModule();

        Assert.AreEqual("Relations", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData, "the seeding behavior persists its flag in SyncData");
    }

    [TestMethod]
    public void TheModule_AppliesPatch107AtGameInit_AndAddsTheSeedingBehaviorOnly()
    {
        var module = new RelationsModule();

        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch107_PersonalRelationNotice", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(PersonalRelationSeedBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count, "TaomDiplomacyModel is hand-wired in SubModule, not declared here");
    }

    [TestMethod]
    public void TheSeamRegistration_LivesInTheRelationsModule_NotInBondsOfBattle()
    {
        var relations = Container();
        var bonds = new Container();
        new BondsOfBattleModule().RegisterServices(bonds);

        Assert.IsTrue(relations.IsRegistered<IRelationGrantService>());
        Assert.IsTrue(relations.IsRegistered<IHeroRelationAdapter>());
        Assert.IsFalse(bonds.IsRegistered<IRelationGrantService>(), "a second default makes every resolve throw");
        Assert.IsFalse(bonds.IsRegistered<IHeroRelationAdapter>(), "a second default makes every resolve throw");
    }

    [TestMethod]
    public void BothModulesTogether_ResolveTheSeamOnce()
    {
        var container = Container();
        new BondsOfBattleModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<TAOM.Features.Execution.IAlignmentService>());

        Assert.IsNotNull(container.Resolve<IRelationGrantService>());
        Assert.IsNotNull(container.Resolve<BattleRelationService>());
    }

    [TestMethod]
    public void EveryServiceTheModuleRegisters_ResolvesFromItsOwnRegistrations()
    {
        var container = Container();

        Assert.IsNotNull(container.Resolve<IRelationGrantService>());
        Assert.IsNotNull(container.Resolve<IPersonalRelationSettingsProvider>());
        Assert.IsNotNull(container.Resolve<IPersonalRelationService>());
        Assert.IsNotNull(container.Resolve<IPersonalRelationSeedAdapter>());
        Assert.IsNotNull(container.Resolve<IPersonalRelationNoticeAdapter>());
        Assert.IsNotNull(container.Resolve<PersonalRelationSeedService>());
    }

    [TestMethod]
    public void ThePersonalRelationService_IsASingleton_SoTheHotPathNeverBuildsOne()
    {
        var container = Container();

        Assert.AreSame(container.Resolve<IPersonalRelationService>(), container.Resolve<IPersonalRelationService>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorForEachCampaign()
    {
        var container = Container();
        var decl = new RelationsModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(PersonalRelationSeedBehavior));
        Assert.AreNotSame(first, second, "a container singleton would carry one campaign's seeded flag into the next");
    }

    // --- composition order ---

    [TestMethod]
    public void SubModule_ConfiguresTheContainer_InOnSubModuleLoad_BeforeOnGameStartResolvesThePersonalRelationService()
    {
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        var load = code.IndexOf("protected override void OnSubModuleLoad()", StringComparison.Ordinal);
        var configure = code.IndexOf("IoC.Configure();", StringComparison.Ordinal);
        var gameStart = code.IndexOf("protected override void OnGameStart(", StringComparison.Ordinal);
        var resolve = code.IndexOf("IoC.Resolve<Features.Relations.IPersonalRelationService>()", StringComparison.Ordinal);

        Assert.IsTrue(load >= 0 && configure > load, "IoC.Configure() is called from OnSubModuleLoad");
        Assert.IsTrue(gameStart > configure, "OnGameStart comes after the Configure call in the file and in the engine's order");
        Assert.IsTrue(resolve > gameStart, "the model is built, and the service resolved, inside OnGameStart");
        Assert.AreEqual(1, code.Split(new[] { "IoC.Resolve<Features.Relations.IPersonalRelationService>()" }, StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void TheModelIsConstructedOnceInSubModule_WithThePersonalRelationService()
    {
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var at = code.IndexOf("new TaomDiplomacyModel(", StringComparison.Ordinal);

        Assert.IsTrue(at >= 0);
        Assert.AreEqual(-1, code.IndexOf("new TaomDiplomacyModel(", at + 1, StringComparison.Ordinal));
        var statement = code.Substring(at, code.IndexOf(';', at) - at);
        StringAssert.Contains(statement, "IPersonalRelationService");
    }
}
