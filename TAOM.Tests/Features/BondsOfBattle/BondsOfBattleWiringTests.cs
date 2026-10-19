using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.BondsOfBattle.Hooks;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BondsOfBattle;

/// <summary>
/// Wiring regression guard for Bonds of Battle. Each piece fails silently when unwired: drop the module line and nothing
/// runs; drop the category and Patch106 never applies; drop the model and the barter keeps vanilla's cap of 3; leave a
/// dependency unregistered and the first battle's resolve throws inside the behavior's catch.
/// </summary>
[TestClass]
public class BondsOfBattleWiringTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void FeatureModules_ListTheBondsOfBattleModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<BondsOfBattleModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNotParked_AndOwnsNoSaveData()
    {
        var module = new BondsOfBattleModule();

        Assert.AreEqual("BondsOfBattle", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
    }

    [TestMethod]
    public void TheModule_AppliesPatch106AtGameInit()
    {
        var decls = new BondsOfBattleModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch106_BondsOfBattle", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_DeclaresTheBarterModelForTheCampaign()
    {
        var models = new BondsOfBattleModule().GameModels.ToList();

        Assert.AreEqual(1, models.Count);
        Assert.AreEqual(typeof(BarterModel), models[0].SlotType);
        Assert.AreEqual(typeof(TaomBarterModel), models[0].ModelType);
        Assert.AreEqual(ModelTarget.Campaign, models[0].Target);
    }

    [TestMethod]
    public void TheModule_AddsOneCampaignBehavior_AndNoMissionBehavior()
    {
        var module = new BondsOfBattleModule();

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(BondsOfBattleBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        var container = new Container();
        new BondsOfBattleModule().RegisterServices(container);
        new TAOM.Features.Relations.RelationsModule().RegisterServices(container); // owns the relation seam
        // The services the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());

        Assert.IsNotNull(container.Resolve<IRelationGrantService>());
        Assert.IsNotNull(container.Resolve<BattleRelationService>());
        Assert.IsNotNull(container.Resolve<TroopHandoverService>());
        Assert.IsNotNull(container.Resolve<BarterOverpayService>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsTheBehaviorFromTheModulesRegistrations()
    {
        var container = new Container();
        var module = new BondsOfBattleModule();
        module.RegisterServices(container);
        new TAOM.Features.Relations.RelationsModule().RegisterServices(container); // owns the relation seam
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());

        Assert.IsNotNull(module.CampaignBehaviors[0].Create(container));
    }

    // --- Patch106 ------------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("RequiresGame")] // reading attributes off every TAOM type loads types from game assemblies the stubs lack
    public void Patch106_IsTheOnlyClassInTheCategory_AndTargetsDoneLogic()
    {
        var classes = AccessTools.GetTypesFromAssembly(typeof(BondsOfBattleModule).Assembly)
            .Where(t => t.GetCustomAttributes<HarmonyPatchCategory>().Any(c => c.info.category == "Patch106_BondsOfBattle"))
            .ToList();

        CollectionAssert.AreEqual(new[] { typeof(PartyScreenLogic_DoneLogic_Patch) }, classes);
        var target = typeof(PartyScreenLogic).GetMethod("DoneLogic", new[] { typeof(bool) });
        Assert.IsNotNull(target, "PartyScreenLogic.DoneLogic(bool) is public");
        Assert.AreEqual(typeof(bool), target.ReturnType);
    }

    [TestMethod]
    public void Patch106_BindsItsStateByTheNamesHarmonyKnows()
    {
        var prefix = typeof(PartyScreenLogic_DoneLogic_Patch).GetMethod("Prefix")!;
        var postfix = typeof(PartyScreenLogic_DoneLogic_Patch).GetMethod("Postfix")!;

        CollectionAssert.AreEqual(new[] { "__instance", "__state" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "__result", "__state" }, postfix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[1].IsOut, "the prefix writes the state before anything can throw");
        Assert.AreEqual(typeof(void), prefix.ReturnType);
        Assert.AreEqual(typeof(void), postfix.ReturnType);
    }

    [TestMethod]
    public void Patch106_PrefixOnAScreenItCannotRead_LeavesTheDefaultState()
    {
        // A null screen throws inside Capture; the prefix must swallow it and hand the postfix "no lord".
        PartyScreenLogic_DoneLogic_Patch.Prefix(null!, out var state);

        Assert.AreEqual(default(HandoverCapture), state);
    }

    [TestMethod]
    public void Patch106_PostfixWhenTheServiceCannotBeResolved_SwallowsTheFault()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // TAOM.IoC is not configured in a test host, so the resolve throws; the party screen must still close.
        PartyScreenLogic_DoneLogic_Patch.Postfix(true, new HandoverCapture("lord_1", "Lord One", 50, false, false));
    }

    // --- the barter model ------------------------------------------------------------------------------------------

    [TestMethod]
    public void TaomBarterModel_OverridesOnlyTheOverpayMethod()
    {
        var overridden = typeof(TaomBarterModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        CollectionAssert.AreEqual(new[] { "CalculateOverpayRelationIncreaseCosts" }, overridden);
    }
}
