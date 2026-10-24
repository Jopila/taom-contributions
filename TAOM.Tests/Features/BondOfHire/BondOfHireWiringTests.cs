using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BondOfHire;
using TAOM.Features.BondOfHire.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// Wiring guards for Bond of Hire. Each piece fails silently when unwired: drop the module line and nothing runs; leave a
/// dependency unregistered and the first signing throws inside the handler's catch. The service holds no per-campaign state, so
/// the behavior built for each campaign is the only thing a campaign needs fresh.
/// </summary>
[TestClass]
public class BondOfHireWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new BondOfHireModule().RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheBondOfHireModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<BondOfHireModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.BondOfHire.BondOfHireModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("BondOfHire"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("BondOfHire"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_PatchesNothing_AndDeclaresNoModel()
    {
        var module = new BondOfHireModule();

        Assert.AreEqual("BondOfHire", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the date is the engine's own saved clan field; nothing of TAOM's is persisted");
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony patch: one event");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_AddsOneCampaignBehavior_BuiltWithNew()
    {
        var module = new BondOfHireModule();
        var src = RepoPaths.ReadSource("Main/Features/BondOfHire/BondOfHireModule.cs", stripComments: true);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(BondOfHireBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsTrue(src.Contains("new BondOfHireBehavior("), "the behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<BondOfHireBehavior>"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<IBondOfHireSettingsProvider>());
        Assert.IsNotNull(container.Resolve<BondOfHireService>());
    }

    [TestMethod]
    public void TheService_IsOneSharedInstance()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<BondOfHireService>(), container.Resolve<BondOfHireService>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorForEachCampaign()
    {
        using var container = BuildContainer();
        var decl = new BondOfHireModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(BondOfHireBehavior));
        Assert.AreNotSame(first, second, "a campaign registers its behaviors' events on the instance, so each campaign needs its own");
    }

    [TestMethod]
    public void BondOfHire_DeclaresNoHarmonyPatch()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "BondOfHire"), "*.cs", SearchOption.AllDirectories);

        Assert.IsTrue(files.Length > 0);
        foreach (var file in files)
            Assert.IsFalse(RepoPaths.MentionsOutsideComments(file, "HarmonyPatch"), file + " declares a Harmony patch");
    }

    [TestMethod]
    public void TheListener_RegistersTheEnginesServiceStartedEvent_AndWrapsTheClanInTheAdapter()
    {
        // The argument is an engine type that cannot be built outside the game, so the expressions are pinned from the source.
        var src = RepoPaths.ReadSource("Main/Features/BondOfHire/Hooks/BondOfHireBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnMercenaryServiceStartedEvent.AddNonSerializedListener(this, OnMercenaryServiceStarted)");
        StringAssert.Contains(src, "new MercenaryClanAdapter(clan)");
    }

    [TestMethod]
    public void EveryServiceCall_SitsInsideATry()
    {
        // An MbEvent invokes its listeners with no try: a fault would reach the engine's hiring code.
        var src = RepoPaths.ReadSource("Main/Features/BondOfHire/Hooks/BondOfHireBehavior.cs", stripComments: true);

        var at = src.IndexOf("_service.OnServiceStarted(", System.StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, "_service.OnServiceStarted( not found");
        var tryAt = src.LastIndexOf("try", at, System.StringComparison.Ordinal);
        var catchAt = src.IndexOf("catch (Exception", at, System.StringComparison.Ordinal);
        Assert.IsTrue(tryAt >= 0 && catchAt > at, "the service call must sit inside a try with a catch");
    }

    // Clan.PlayerClan is a computed getter that throws without a campaign (adapters.md): the campaign is checked first.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheAdapter_WithNoCampaign_SaysNotThePlayerClan_WithoutThrowing()
    {
        var clan = (TaleWorlds.CampaignSystem.Clan)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TaleWorlds.CampaignSystem.Clan));

        Assert.IsFalse(new TAOM.Adapters.MercenaryClanAdapter(clan).IsPlayerClan);
    }

    [TestMethod]
    public void TheAdapter_RefusesANullClan()
    {
        Assert.ThrowsException<System.ArgumentNullException>(() => new TAOM.Adapters.MercenaryClanAdapter(null!));
    }

    [TestMethod]
    public void TheAdapter_ReadsAndWritesTheEnginesDate()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/MercenaryClanAdapter.cs", stripComments: true);

        StringAssert.Contains(src, "ShouldStayInKingdomUntil.RemainingDaysFromNow");
        StringAssert.Contains(src, "ShouldStayInKingdomUntil = CampaignTime.DaysFromNow(days)");
    }
}
