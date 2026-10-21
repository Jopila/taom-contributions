using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LongMemory;
using TAOM.Features.LongMemory.Hooks;
using TAOM.Features.Relations;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// Wiring guards for Long Memory. Each piece fails silently when unwired: drop the module line and nothing runs; leave a
/// dependency unregistered and the first raid throws inside a handler's catch. The service holds no per-campaign state, so the
/// behavior built for each campaign is the only thing a campaign needs fresh.
/// </summary>
[TestClass]
public class LongMemoryWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new LongMemoryModule().RegisterServices(container);
        new RelationsModule().RegisterServices(container); // owns the relation seam
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheLongMemoryModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LongMemoryModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.LongMemory.LongMemoryModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("LongMemory"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("LongMemory"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_PatchesNothing_AndDeclaresNoModel()
    {
        var module = new LongMemoryModule();

        Assert.AreEqual("LongMemory", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony patch: four events");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_AddsOneCampaignBehavior_BuiltWithNew()
    {
        var module = new LongMemoryModule();
        var src = RepoPaths.ReadSource("Main/Features/LongMemory/LongMemoryModule.cs", stripComments: true);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(LongMemoryBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsTrue(src.Contains("new LongMemoryBehavior("), "the behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<LongMemoryBehavior>"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<ILongMemorySettingsProvider>());
        Assert.IsNotNull(container.Resolve<ILongMemoryVillageAdapter>());
        Assert.IsNotNull(container.Resolve<ILongMemoryNoticeAdapter>());
        Assert.IsNotNull(container.Resolve<LongMemoryService>());
    }

    [TestMethod]
    public void TheService_IsOneSharedInstance()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<LongMemoryService>(), container.Resolve<LongMemoryService>());
    }

    [TestMethod]
    public void TheModule_DoesNotRegisterTheSharedSeamsAgain()
    {
        var src = RepoPaths.ReadSource("Main/Features/LongMemory/LongMemoryModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IRelationGrantService,"), "RelationsModule owns the seam; a second default makes every resolve throw");
        Assert.IsFalse(src.Contains("IHeroRelationAdapter,"));
        Assert.IsFalse(src.Contains("IAlignmentService,"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorForEachCampaign()
    {
        using var container = BuildContainer();
        var decl = new LongMemoryModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(LongMemoryBehavior));
        Assert.AreNotSame(first, second, "a campaign registers its behaviors' events on the instance, so each campaign needs its own");
    }

    [TestMethod]
    public void LongMemory_DeclaresNoHarmonyPatch()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "LongMemory"), "*.cs", SearchOption.AllDirectories);

        Assert.IsTrue(files.Length > 0);
        foreach (var file in files)
            Assert.IsFalse(RepoPaths.MentionsOutsideComments(file, "HarmonyPatch"), file + " declares a Harmony patch");
    }

    [TestMethod]
    public void LongMemory_BuildsNoSideThatIsNotThroughFactionSides()
    {
        // #957: a side is the id and the culture of ONE entity, built in exactly one place. The service and behavior never build
        // a SideIds or call the two-argument ResolveSide; the adapter reads sides through FactionSides only.
        foreach (var file in new[]
        {
            "Main/Features/LongMemory/LongMemoryService.cs",
            "Main/Features/LongMemory/Hooks/LongMemoryBehavior.cs",
            "Main/Adapters/LongMemoryVillageAdapter.cs",
        })
        {
            var src = RepoPaths.ReadSource(file, stripComments: true);
            Assert.IsFalse(src.Contains("new SideIds("), file + " builds a SideIds");
            Assert.IsFalse(Regex.IsMatch(src, @"ResolveSide\([^()]*,"), file + " calls the two-argument ResolveSide");
        }
        var adapter = RepoPaths.ReadSource("Main/Adapters/LongMemoryVillageAdapter.cs", stripComments: true);
        StringAssert.Contains(adapter, "FactionSides.OfPlayer()");
        StringAssert.Contains(adapter, "FactionSides.Of(");
    }

    [TestMethod]
    public void TheVillageAdapter_ReadsAllThreeEventKinds_FromOneComponent()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/LongMemoryVillageAdapter.cs", stripComments: true);

        StringAssert.Contains(src, "as RaidEventComponent");
        StringAssert.Contains(src, "AttackerSide");
        StringAssert.Contains(src, "LeaderParty");
        StringAssert.Contains(src, "== Clan.PlayerClan");
    }

    // The adapter reads engine objects, so its two early-outs are pinned from the source (the map-event leader rule and the menu rule
    // that mirrors it).
    [TestMethod]
    public void TheMapEventRead_LeavesBeforeAnyNameSideOrNotableRead_UnlessThePlayersPartyLeads()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/LongMemoryVillageAdapter.cs", stripComments: true);
        var body = MethodBody(src, "ReadMapEvent");

        var gate = Regex.Match(body, @"if \(main == null \|\| !ReferenceEquals\(mapEvent\.AttackerSide\?\.LeaderParty, main\)\)\s*return null;");
        Assert.IsTrue(gate.Success, "an act the player's own party does not lead returns null from the map-event read");
        var read = body.IndexOf("Read(settlement", StringComparison.Ordinal);
        Assert.IsTrue(read > gate.Index, "the leader gate sits before the village is read");
        Assert.IsFalse(body.Contains("settlement.Name") || body.Contains("FactionSides") || body.Contains("Notables"),
            "no name, side or notable is read in the map-event method itself");
    }

    [TestMethod]
    public void TheMenuRead_LeavesOnNoMainParty_BeforeItReadsTheCurrentSettlement_AndPinsTheLeadsRule()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/LongMemoryVillageAdapter.cs", stripComments: true);
        var body = MethodBody(src, "ReadCurrentVillage");

        var gate = Regex.Match(body, @"if \(main == null\)\s*return null;");
        Assert.IsTrue(gate.Success, "no main party returns null");
        var settlement = body.IndexOf("Settlement.CurrentSettlement", StringComparison.Ordinal);
        Assert.IsTrue(settlement > gate.Index, "the main-party gate sits before the current settlement is read");
        StringAssert.Contains(body, "main.AttachedTo == null && (main.Army == null || main.Army.LeaderParty == main)");
    }

    // The text of the first method with this name: from its signature to the next member at the same indent.
    private static string MethodBody(string src, string name)
    {
        var at = src.IndexOf(" " + name + "(", StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, name + " not found");
        var next = src.IndexOf("\n    public ", at + 1, StringComparison.Ordinal);
        var nextPrivate = src.IndexOf("\n    private ", at + 1, StringComparison.Ordinal);
        var end = new[] { next, nextPrivate }.Where(i => i > 0).DefaultIfEmpty(src.Length).Min();
        return src.Substring(at, end - at);
    }

    [TestMethod]
    public void TheListener_PassesTheOptionsIdString_AndRegistersTheFourEvents()
    {
        // The arguments come from engine types that cannot be built outside the game, so the expressions are pinned from the source.
        var src = RepoPaths.ReadSource("Main/Features/LongMemory/Hooks/LongMemoryBehavior.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"HandleMenuOption\(\s*option\?\.IdString\s*\)"), "the listener must pass the selected option's IdString");
        foreach (var registration in new[]
        {
            "CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted)",
            "CampaignEvents.ForceSuppliesCompletedEvent.AddNonSerializedListener(this, OnForceSuppliesCompleted)",
            "CampaignEvents.ForceVolunteersCompletedEvent.AddNonSerializedListener(this, OnForceVolunteersCompleted)",
            "CampaignEvents.GameMenuOptionSelectedEvent.AddNonSerializedListener(this, OnGameMenuOptionSelected)",
        })
            StringAssert.Contains(src, registration);
        Assert.IsTrue(Regex.IsMatch(src, @"side\s*==\s*BattleSideEnum\.Attacker"), "a coercion counts only when the attackers won");
    }

    [TestMethod]
    public void EveryServiceCall_SitsInsideATry()
    {
        // An MbEvent invokes its listeners with no try: a fault would reach the engine's raid or menu code.
        var src = RepoPaths.ReadSource("Main/Features/LongMemory/Hooks/LongMemoryBehavior.cs", stripComments: true);

        foreach (var call in new[] { "_service.OnRaidCompleted(", "_service.OnCoercionCompleted(", "_service.OnMenuOptionSelected(" })
        {
            var at = src.IndexOf(call, StringComparison.Ordinal);
            Assert.IsTrue(at >= 0, call + " not found");
            var tryAt = src.LastIndexOf("try", at, StringComparison.Ordinal);
            var catchAt = src.IndexOf("catch (Exception", at, StringComparison.Ordinal);
            Assert.IsTrue(tryAt >= 0 && catchAt > at, call + " must sit inside a try with a catch");
        }
    }
}
