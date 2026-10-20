using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.FellFoes;
using TAOM.Features.FellFoes.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FellFoes;

/// <summary>
/// Wiring guards for Fell Foes (#893). No patch and no model reaches the feature: the module list adds the
/// mission behavior, the behavior ticks the service, and the adapter is the only code that touches an agent.
/// Drop any link and battles silently run as vanilla.
/// </summary>
[TestClass]
public class FellFoesWiringTests
{
    private const string LogicPath = "Main/Features/FellFoes/Hooks/FellFoesMissionLogic.cs";

    [TestMethod]
    public void FeatureModules_ListTheFellFoesModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<FellFoesModule>().Count(),
            "FellFoesModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior_AndNothingElse()
    {
        var module = new FellFoesModule();

        Assert.AreEqual("FellFoes", module.Id);
        Assert.AreEqual(1, module.MissionBehaviors.Count);
        Assert.AreEqual(typeof(FellFoesMissionLogic), module.MissionBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "Fell Foes ships no Harmony patch");
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_RegistersEverythingTheBehaviorNeeds()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new FellFoesModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<IFellFoesAdapter>());
        Assert.IsNotNull(container.Resolve<IFellFoesSettingsProvider>());
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public void Gate_StandsDownInAnyCoopSession_OrWhereTheHostOwnsTheBattle(bool sessionActive, bool deferToHost, bool standsDown)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsSessionActive.Returns(sessionActive);
        coop.ShouldDeferToHost.Returns(deferToHost);

        Assert.AreEqual(standsDown, FellFoesMissionGate.IsStandingDown(coop));
    }

    [TestMethod]
    public void Gate_ReusesTheBattleAllowlist_NotABlocklist()
    {
        var src = RepoPaths.ReadSource("Main/Features/FellFoes/Hooks/FellFoesMissionGate.cs", stripComments: true);

        StringAssert.Contains(src, "MountDespawnMissionGate.IsEligible(");
    }

    [TestMethod]
    public void Gate_ReadsMissionEnded_SoNothingIsWrittenOnceTheResultIsDecided()
    {
        var src = RepoPaths.ReadSource("Main/Features/FellFoes/Hooks/FellFoesMissionGate.cs", stripComments: true);

        StringAssert.Contains(src, ".MissionEnded");
    }

    [TestMethod]
    public void Behavior_OverridesTheTeardowns_AndReleasesInBoth()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);

        StringAssert.Contains(src, "protected override void OnEndMission() => Release();");
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(src, @"OnRemoveBehavior\(\)\s*\{\s*Release\(\);\s*base\.OnRemoveBehavior\(\);"),
            "removal releases before the base call");
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(src, @"void Release\(\)\s*\{\s*try\s*\{\s*_service\.ReleaseHolds\(\);"),
            "Release asks the service to give every held archer back");
    }

    [TestMethod]
    public void Behavior_IsALogic_SoVanillaAddsItToTheLogicList()
    {
        Assert.IsTrue(typeof(TaleWorlds.MountAndBlade.MissionLogic).IsAssignableFrom(typeof(FellFoesMissionLogic)));
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);
        Assert.IsFalse(src.Contains("override MissionBehaviorType BehaviorType"), "MissionLogic already reports Logic");
    }

    [TestMethod]
    public void Behavior_IsAThinEntryPoint_WithNoSubscriptionsAndNoBannedConstructs()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);
        var raw = RepoPaths.ReadSource(LogicPath, stripComments: false);

        Assert.IsTrue(raw.Split('\n').Length < 150, "ADR-002: entry points stay under 150 lines");
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(src, @"\.\w+\s*\+="),
            "no event subscriptions: every read and write is in OnMissionTick");
        Assert.IsFalse(src.Contains("OnBehaviorInitialize"), "never fires for a TAOM behavior (#606)");
        Assert.IsFalse(raw.Contains("#region") || raw.Contains("[Obsolete") || raw.Contains("#if DEBUG"));
        Assert.IsFalse(src.Contains(".Index"), "never key on an agent index");
    }

    [TestMethod]
    public void EveryMission_GetsItsOwnBehaviorAndItsOwnState()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new FellFoesModule().RegisterServices(container);
        var decl = new FellFoesModule().MissionBehaviors[0];

        var first = (FellFoesMissionLogic)decl.Create(null!, container);
        var second = (FellFoesMissionLogic)decl.Create(null!, container);

        Assert.AreNotSame(first, second);
        var field = typeof(FellFoesMissionLogic).GetField("_service", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.AreNotSame(field.GetValue(first), field.GetValue(second), "state is per mission: a shared service would carry one battle into the next");
    }

    [TestMethod]
    public void CoopClassification_ExcludesTheDebugLog_AndCoversTheRest()
    {
        var mine = typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("FellFoes", System.StringComparison.Ordinal)).ToList();

        Assert.AreEqual(9, mine.Count);
        foreach (var p in mine)
            Assert.AreEqual(p.Name != nameof(TaomSettings.FellFoesDebug), CoopSettingsRelevance.IsSimulationRelevant(p), p.Name);
    }

    [TestMethod]
    public void Adapter_GatesEveryWriteOnTheLiveOccupant_AndNotesTheThread()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/FellFoesAdapter.cs", stripComments: true);

        StringAssert.Contains(src, "AgentSlotIdentity.IsCurrentOccupant(");
        Assert.AreEqual(5, src.Split(new[] { "MissionThreadGuard.NoteCall(" }, System.StringSplitOptions.None).Length - 1,
            "one tripwire per write");
        StringAssert.Contains(src, "TeamTacticProbe.IsHorseStanding(");
        Assert.IsFalse(src.Contains(".Index"), "handles are agent objects, never indices");
    }

    [TestMethod]
    public void Rules_CarryNoEngineTypes()
    {
        foreach (var path in new[]
                 {
                     "Main/Features/FellFoes/FellFoesRules.cs", "Main/Features/FellFoes/FellFoesService.cs",
                     "Main/Features/FellFoes/Domain/EnemyAgentView.cs", "Main/Features/FellFoes/Domain/FellFoesScan.cs",
                     "Main/Features/FellFoes/Domain/FellFoesMissionState.cs", "Main/Features/FellFoes/Domain/FellFoesTunables.cs",
                 })
        {
            var src = RepoPaths.ReadSource(path, stripComments: true);
            Assert.IsFalse(src.Contains("using TaleWorlds"), path + " is a service: ADR-007 keeps TaleWorlds types out");
        }
    }
}
