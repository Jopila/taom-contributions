using System;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.FromTheAshes;
using TAOM.Features.FromTheAshes.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>
/// Wiring guard for From the Ashes (#856): the module line, its save footprint, the per-campaign behavior reset, and the
/// two menu rules that bit the field camp (#567: a false wait-menu condition hides every option; only an isLeave option
/// gets EndWait). The menu pins read the Hooks sources, comment-stripped, because the callbacks need a running campaign.
/// </summary>
[TestClass]
public class FromTheAshesWiringTests
{
    private const string ModulePath = "Main/Features/FromTheAshes/FromTheAshesModule.cs";
    private const string BehaviorPath = "Main/Features/FromTheAshes/Hooks/FromTheAshesBehavior.cs";

    private static readonly Regex InlinePerCampaign =
        new(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+FromTheAshesBehavior\(");

    private static readonly Regex AnyConstruction = new(@"new\s+FromTheAshesBehavior\(");

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<FromTheAshesModule>().Count(),
            "FromTheAshesModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void TheModule_IsNamedFromTheAshes_NotParked_OwnsSaveData_DeclaresOneBehavior()
    {
        var module = new FromTheAshesModule();

        Assert.AreEqual("FromTheAshes", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData, "the job is saved, so a fault fails closed");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the feature is menus and a hook-free service");
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody_ExactlyOnce()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.AreEqual(1, InlinePerCampaign.Matches(src).Count, "the factory must build the behavior with new");
        Assert.AreEqual(1, AnyConstruction.Matches(src).Count,
            "its constructor is the per-campaign reset of the job store: a cached instance would carry one campaign's job into the next");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !path.EndsWith("FromTheAshesModule.cs") && !path.EndsWith("FromTheAshesBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "FromTheAshesBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("FromTheAshes"), "the module registers everything; a hand registration doubles it");
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.RegisterInstance(Substitute.For<IEncounterAdapter>());
        new FromTheAshesModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<FromTheAshesService>());
        Assert.AreSame(container.Resolve<AshesJobStore>(), container.Resolve<AshesJobStore>(), "one store: the service and the behavior share it");
        Assert.IsInstanceOfType(container.Resolve<IAshesWorldAdapter>(), typeof(AshesWorldAdapter));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Module_RegistersTheServiceGraph_AndTheFactoryBuildsAFreshBehavior()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.RegisterInstance(Substitute.For<IEncounterAdapter>());
        var module = new FromTheAshesModule();
        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();

        Assert.AreEqual(typeof(FromTheAshesBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(FromTheAshesBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
    }

    [TestMethod]
    public void TheWaitMenuCondition_IsTheLiteralTrue()
    {
        // GameMenu.GetMenuOptionConditionsHold ANDs a wait menu's condition with every option's own, Leave included:
        // anything but `args => true` hides Stop the moment it turns false and strands the player (#567).
        var src = HooksSource();

        var start = src.IndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the wait menu is no longer registered with AddWaitGameMenu");
        Assert.AreEqual(start, src.LastIndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal), "one wait menu, one registration site");
        var end = src.IndexOf(");", start, StringComparison.Ordinal);
        var call = src.Substring(start, end - start);

        StringAssert.Contains(call, "args => true,");
        StringAssert.Contains(call, "WaitMenuShowProgressAndHoursOption");
        StringAssert.Contains(call, "\"{=!}{TAOM_ASHES_STATUS}\"");
        Assert.AreEqual(1, Regex.Matches(src, @"taom_ashes_wait""").Count, "one wait menu id, not one per job kind");
    }

    [TestMethod]
    public void StopIsTheOnlyLeaveOption()
    {
        var src = HooksSource();

        Assert.AreEqual(1, Regex.Matches(src, @"isLeave:\s*true").Count, "only Stop is a leave option");
        Assert.AreEqual(1, Regex.Matches(src, @"""taom_ashes_stop""").Count);
        var stop = src.Substring(src.IndexOf("\"taom_ashes_stop\"", StringComparison.Ordinal));
        Assert.IsTrue(Regex.IsMatch(stop.Substring(0, stop.IndexOf("index: 0);", StringComparison.Ordinal)), @"isLeave:\s*true"),
            "the isLeave flag belongs to the Stop option");
        Assert.AreEqual(3, Regex.Matches(src, @"isLeave:\s*false").Count, "the three start options stay ordinary");
    }

    [TestMethod]
    public void TheStartOptions_SitOnTheThreeVanillaMenus_AtIndexZero()
    {
        var src = Source("FromTheAshesStartOptions.cs");

        StringAssert.Contains(src, "\"village_looted\"");
        StringAssert.Contains(src, "\"town_keep\"");
        StringAssert.Contains(src, "\"castle\"");
        Assert.AreEqual(3, Regex.Matches(src, @"index:\s*0").Count, "three start options at index 0");
    }

    [TestMethod]
    public void NoWaitCallback_SwitchesIntoItsOwnMenu_AndTheOnlyOtherSwitchesAreTheLanding()
    {
        // SwitchTo(same id) re-initialises a wait menu and restarts the wait; the one switch into the wait id is from Start.
        var wait = Source("FromTheAshesWaitMenu.cs");
        var options = Source("FromTheAshesStartOptions.cs");

        var landing = Source("FromTheAshesLanding.cs");
        Assert.AreEqual(0, Regex.Matches(wait, @"\.SwitchTo\(").Count, "the wait menu switches only through the landing");
        Assert.AreEqual(2, Regex.Matches(landing, @"\.SwitchTo\(").Count, "the landing switches only to the engine's choice and to the settlement's menu");
        Assert.IsFalse(Regex.IsMatch(landing, @"SwitchTo\(\s*FromTheAshesWaitMenu\.WaitMenuId"), "the landing never switches into the wait menu");
        Assert.AreEqual(1, Regex.Matches(options, @"\.SwitchTo\(").Count);
        StringAssert.Contains(options, "SwitchTo(FromTheAshesWaitMenu.WaitMenuId)");
    }

    [TestMethod]
    public void TheLanding_ConsultsTheEnginesChoiceFirst_ThenTheSettlementMenu_ThenFinishesTheEncounter()
    {
        var land = Source("FromTheAshesLanding.cs");

        var generic = land.IndexOf("GenericStateMenuId", StringComparison.Ordinal);
        var own = land.IndexOf("CurrentSettlementMenuId", StringComparison.Ordinal);
        var finish = land.IndexOf("Finish(forcePlayerOutFromSettlement: true)", StringComparison.Ordinal);
        var exit = land.IndexOf("ExitToLast()", StringComparison.Ordinal);
        Assert.IsTrue(generic >= 0 && generic < own, "a besieged fief's siege menu comes before the settlement's own menu");
        Assert.IsTrue(own < finish, "inside a settlement the last resort is a finished encounter");
        Assert.IsTrue(finish < exit, "ExitToLast is only for a party in no settlement");
    }

    [TestMethod]
    public void NoBareExitToLast_RunsInsideASettlement()
    {
        // A village has no generic-state menu, so ExitToLast from the wait menu strands the player (the soft-lock).
        var src = HooksSource();

        Assert.AreEqual(1, Regex.Matches(src, @"ExitToLast\(").Count, "one exit, inside the landing");
        var landing = Source("FromTheAshesLanding.cs");
        var guard = landing.IndexOf("_encounter.IsInsideSettlement", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && guard < landing.IndexOf("ExitToLast(", StringComparison.Ordinal),
            "the settlement check precedes the only ExitToLast");
    }

    [TestMethod]
    public void EveryExit_GoesThroughTheLanding()
    {
        var wait = Source("FromTheAshesWaitMenu.cs");
        var reconcile = Source("FromTheAshesReconcile.cs");

        // init (non-running), complete and stop land; a forced end of the tick, a fault, and the hourly look, leave the wait first.
        Assert.AreEqual(3, Regex.Matches(wait, @"_landing\.Land\(\)").Count);
        Assert.AreEqual(2, Regex.Matches(wait, @"_landing\.LeaveWaitMenu\(").Count);
        Assert.AreEqual(1, Regex.Matches(reconcile, @"_landing\.LeaveWaitMenu\(").Count);
        foreach (var src in new[] { wait, reconcile, Source("FromTheAshesStartOptions.cs") })
            Assert.IsFalse(Regex.IsMatch(src, @"ExitToLast|\.SwitchTo\(\s*[a-z]|Finish\("), "only the landing exits");
    }

    [TestMethod]
    public void TheWaitTick_HasItsOwnTryCatchAndNoLambda()
    {
        var wait = Source("FromTheAshesWaitMenu.cs");
        var tick = wait.Substring(wait.IndexOf("private void OnWaitTick(", StringComparison.Ordinal));
        tick = tick.Substring(0, tick.IndexOf("private void OnWaitComplete(", StringComparison.Ordinal));

        StringAssert.Contains(tick, "catch (Exception ex)");
        StringAssert.Contains(tick, "Fault(\"wait tick\"");
        Assert.IsFalse(tick.Contains("=>"), "a lambda per frame allocates on the hottest callback");
    }

    [TestMethod]
    public void EachHookClass_CatchesAndLogsThroughOneLatch()
    {
        var wait = Source("FromTheAshesWaitMenu.cs");
        var reconcile = Source("FromTheAshesReconcile.cs");
        var options = Source("FromTheAshesStartOptions.cs");

        Assert.AreEqual(2, Regex.Matches(wait, @"catch \(Exception ex\)").Count, "the tick, and the Guard that init, complete and stop share");
        Assert.AreEqual(1, Regex.Matches(wait, @"Fault\(""").Count, "the tick; the Guard passes its callback name on");
        Assert.AreEqual(3, Regex.Matches(wait, @"Guard\(""").Count, "init, complete, stop");
        Assert.AreEqual(2, Regex.Matches(reconcile, @"catch \(Exception ex\)").Count, "the hourly reconcile and the map event listener");
        Assert.AreEqual(2, Regex.Matches(reconcile, @"Fault\(""").Count);
        Assert.AreEqual(1, Regex.Matches(reconcile, @"_logger\.LogError").Count);
        Assert.AreEqual(1, Regex.Matches(wait, @"_logger\.LogError").Count);
        Assert.AreEqual(2, Regex.Matches(options, @"catch \(Exception ex\)").Count, "the start condition and the start");
        Assert.AreEqual(2, Regex.Matches(options, @"LogOnce\(""").Count);
        Assert.AreEqual(1, Regex.Matches(options, @"_logger\.LogError").Count);
        Assert.IsFalse(Regex.IsMatch(wait + options + reconcile, @"catch \(Exception inner\)"), "no nested catch around the fault path");
        foreach (var src in new[] { wait, reconcile })
        {
            StringAssert.Contains(src, "and the job was dropped");
            StringAssert.Contains(src, "no job was running");
        }

        StringAssert.Contains(options, "the job was dropped");
        StringAssert.Contains(options, "no job was touched");
    }

    [TestMethod]
    public void Start_AbandonsTheJob_WhenTheWaitMenuDoesNotOpen()
    {
        var options = Source("FromTheAshesStartOptions.cs");
        var start = options.Substring(options.IndexOf("private void Start(", StringComparison.Ordinal));

        var switchTo = start.IndexOf("SwitchTo(FromTheAshesWaitMenu.WaitMenuId)", StringComparison.Ordinal);
        var abandon = start.IndexOf("_service.Abandon()", switchTo, StringComparison.Ordinal);
        Assert.IsTrue(switchTo >= 0 && abandon > switchTo, "a failed SwitchTo drops the stored (and paid) job");
    }

    [TestMethod]
    public void TheBehavior_ListensToTheHourlyTick_AndToMapEvents()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, _reconcile.OnHourlyTick)");
        StringAssert.Contains(src, "CampaignEvents.MapEventStarted.AddNonSerializedListener(this, _reconcile.OnMapEventStarted)");
    }

    [TestMethod]
    public void EveryHooksFile_StaysUnder150Lines()
    {
        var files = System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FromTheAshes", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 6);
        foreach (var file in files)
            Assert.IsTrue(System.IO.File.ReadAllLines(file).Length < 150, "ADR-002: " + System.IO.Path.GetFileName(file) + " is 150 lines or more");
    }

    [TestMethod]
    public void TheBehavior_HasNoGameLoadedHandler()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        Assert.IsFalse(src.Contains("OnGameLoaded"), "a throw out of a load-phase handler loops the loading screen (lessons/campaign-mechanics.md)");
    }

    [TestMethod]
    public void TheBehavior_SavesTheFivePrimitiveKeys_InBothDirections()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        foreach (var key in new[] { "_taomAshesSite", "_taomAshesKind", "_taomAshesStart", "_taomAshesHours", "_taomAshesCredited" })
            Assert.AreEqual(2, Regex.Matches(src, "\"" + key + "\"").Count, key + " must be synced once on load and once on save");
        StringAssert.Contains(src, "var start = 0d;");
        Assert.IsFalse(src.Contains("CampaignTime"), "the start is saved as a double of hours, with no CampaignTime conversion");
    }

    private static string Source(string hooksFile) =>
        RepoPaths.ReadSource("Main/Features/FromTheAshes/Hooks/" + hooksFile, stripComments: true);

    private static string HooksSource() => string.Concat(
        System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FromTheAshes", "Hooks"), "*.cs")
            .Select(path => Source(System.IO.Path.GetFileName(path))));
}
