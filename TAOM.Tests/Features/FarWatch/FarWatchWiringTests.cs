using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;
using TAOM.Features.HostReview;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// Wiring guard for Far Watch (#847): the module line and its position after Host Review, its save footprint, the per-campaign
/// behavior reset, the camp menu order, and the menu rules that bit the field camp (#567: a false wait-menu condition hides
/// every option; only an isLeave option gets EndWait). The menu pins read the Hooks sources, comment-stripped, because the
/// callbacks need a running campaign; the order of the camp options is composed from the indexes in the sources and the two
/// engine facts pinned in FarWatchEngineFactsTests (AddOption's Count >= index rule, newest-first listeners).
/// </summary>
[TestClass]
public class FarWatchWiringTests
{
    private const string ModulePath = "Main/Features/FarWatch/FarWatchModule.cs";
    private const string BehaviorPath = "Main/Features/FarWatch/Hooks/FarWatchBehavior.cs";

    private static readonly Regex InlinePerCampaign = new(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+FarWatchBehavior\(");
    private static readonly Regex AnyConstruction = new(@"new\s+FarWatchBehavior\(");

    private static string Hooks(string file) => RepoPaths.ReadSource("Main/Features/FarWatch/Hooks/" + file, stripComments: true);

    private static string AllHooks() => string.Concat(
        System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FarWatch", "Hooks"), "*.cs")
            .Select(path => Hooks(System.IO.Path.GetFileName(path))));

    private static Container NewContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IHiddenHostsSettings>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<ICampSettingsProvider>());
        container.RegisterInstance(Substitute.For<ICampService>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.RegisterInstance(Substitute.For<IPartyIntelAdapter>());
        container.Register<HiddenHostsService>(Reuse.Singleton);
        new FarWatchModule().RegisterServices(container);
        return container;
    }

    // ---- The module

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce_AfterHostReview()
    {
        var all = FeatureModules.All;
        Assert.AreEqual(1, all.OfType<FarWatchModule>().Count(), "FarWatchModule must be listed exactly once in Main/Composition/FeatureModules.cs");
        int review = Array.FindIndex(all, m => m is HostReviewModule);
        int scout = Array.FindIndex(all, m => m is FarWatchModule);
        Assert.IsTrue(review >= 0 && scout > review,
            "both modules insert a camp option at index 5; the later listener runs first, so Far Watch goes after Host Review to end up below it");
    }

    [TestMethod]
    public void TheModule_IsNamedFarWatch_NotParked_OwnsSaveData_DeclaresOneBehavior_AndNoPatches()
    {
        var module = new FarWatchModule();

        Assert.AreEqual("FarWatch", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData, "the ride, the sight, the rest and the names are saved, so a fault fails closed");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(FarWatchBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the sight rides the contributor seam, the menus are plain callbacks");
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody_ExactlyOnce()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.AreEqual(1, InlinePerCampaign.Matches(src).Count, "the factory must build the behavior with new");
        Assert.AreEqual(1, AnyConstruction.Matches(src).Count,
            "its constructor is the per-campaign reset of the store: a cached instance would carry one campaign's ride into the next");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !path.EndsWith("FarWatchModule.cs") && !path.EndsWith("FarWatchBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "FarWatchBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void TheBehaviorsConstructor_ClearsTheStore_AndHoldsNoCachedEngineField()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "_store.ResetForNewSession();");
        Assert.IsFalse(Regex.IsMatch(src, @"IoC\.Resolve|static\s+(?!readonly)\w+\s+_\w+\s*[;=]"), "no service locator and no static field in the behavior");
    }

    [TestMethod]
    public void IoC_AndSubModule_DoNotMentionTheFeature()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("FarWatch"), "the module registers everything; a hand registration doubles it");
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer_AndTheSeamIsTheStore()
    {
        using var container = NewContainer();

        Assert.IsNotNull(container.Resolve<FarWatchService>());
        Assert.AreSame(container.Resolve<FarWatchStore>(), container.Resolve<FarWatchStore>(), "one store: the service and the behavior share it");
        Assert.AreSame(container.Resolve<FarWatchStore>(), container.Resolve<IScoutIntelQuery>(), "Hidden Hosts reads the very store the feature writes");
        Assert.IsInstanceOfType(container.Resolve<IFarWatchWorldAdapter>(), typeof(FarWatchWorldAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFarWatchNotifier>(), typeof(FarWatchNotifier));
    }

    [TestMethod]
    public void TheSightContributor_IsRegisteredAsAnIPartySpottingContributor_BesideTheLookout()
    {
        using var container = NewContainer();
        container.Register<IPartySpottingContributor, LookoutSpottingContributor>(Reuse.Singleton);

        var all = container.ResolveMany<IPartySpottingContributor>().ToList();

        Assert.AreEqual(2, all.Count, "a second Register appends (the lookout precedent), so both reach the model");
        Assert.AreEqual(1, all.OfType<FarWatchSightContributor>().Count());
    }

    [TestMethod]
    public void TheVisibilityModel_IsStillBuiltWithEveryRegisteredContributor_ByTheCampaignStart()
    {
        // Far Watch adds its contributor by registration alone; this is the line that hands every registration to the model.
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(src, "new TaomMapVisibilityModel(careerPassives, IoC.ResolveAll<TAOM.Core.Domain.IPartySpottingContributor>())");
        Assert.IsNotNull(typeof(TAOM.Features.CareerSystem.Models.TaomMapVisibilityModel).GetConstructor(
            new[] { typeof(TAOM.Features.CareerSystem.ICareerPassiveService), typeof(IEnumerable<IPartySpottingContributor>) }),
            "SubModule.cs is single-owner: the constructor it calls must not change shape");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BehaviorDecl_BuildsAFreshBehaviorPerCampaignStart()
    {
        using var container = NewContainer();
        container.RegisterInstance(Substitute.For<IFarWatchWorldAdapter>(), IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<IFarWatchNotifier>(), IfAlreadyRegistered.Replace);
        var decl = new FarWatchModule().CampaignBehaviors.Single();

        Assert.IsInstanceOfType(decl.Create(container), typeof(FarWatchBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
    }

    // ---- The camp menu order: Refuge 4, Review 5, Scout 6, Work 7, Leave 8

    private static int IndexIn(string path, string optionIdOrAnchor)
    {
        var src = RepoPaths.ReadSource(path, stripComments: true);
        int at = src.IndexOf(optionIdOrAnchor, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, optionIdOrAnchor + " is not in " + path);
        var m = Regex.Match(src.Substring(at), @"index:\s*(\d+)");
        Assert.IsTrue(m.Success, "no index after " + optionIdOrAnchor);
        return int.Parse(m.Groups[1].Value);
    }

    [TestMethod]
    public void TheCampOptionOrder_IsRefuge_Review_Scout_Leave_BuiltFromTheSourcesIndexesAndTheEnginesRules()
    {
        int forage = IndexIn("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", "\"taom_fc_forage\"");
        int fortify = IndexIn("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", "\"taom_fc_fortify\"");
        int supplies = IndexIn("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", "\"taom_fc_supplies\"");
        int breakCamp = IndexIn("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", "\"taom_fc_break\"");
        int leave = IndexIn("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", "\"taom_fc_camp_leave\"");
        int refuge = IndexIn("Main/Features/Refuge/Hooks/RefugeMenuController.cs", "\"taom_rf_found\"");
        int review = IndexIn("Main/Features/HostReview/Hooks/HostReviewCampaignBehavior.cs", "CampSubMenuId, OptionId");
        int scout = IndexIn("Main/Features/FarWatch/Hooks/FarWatchCampOption.cs", "CampSubMenuId, OptionId");
        int work = IndexIn("Main/Features/FieldCamp/Activities/Hooks/CampWorkMenu.cs", "CampSubMenuId, OptionId");

        // The engine's rule (GameMenu.AddOption): insert at the index when Count >= index, else append. Session listeners of one
        // event run newest first, and the modules' behaviors are added in list order, so Far Watch's after-launch listener runs
        // first, then Host Review's; camp work (#860) is listed after Far Watch, so its listener runs before both. FieldCamp builds
        // its panel and Refuge inserts in OnSessionLaunched, before any of them.
        var menu = new List<string>();
        void Add(string id, int index)
        {
            if (index >= 0 && menu.Count >= index) menu.Insert(index, id);
            else menu.Add(id);
        }

        Add("forage", forage);
        Add("fortify", fortify);
        Add("supplies", supplies);
        Add("break", breakCamp);
        Add("leave", leave);
        Add("refuge", refuge);
        Add("work", work);
        Add("scout", scout);
        Add("review", review);

        CollectionAssert.AreEqual(new[] { "forage", "fortify", "supplies", "break", "refuge", "review", "scout", "work", "leave" }, menu,
            "Leave stays last; Work sits under Scout, which sits under Review, which sits under Refuge");
    }

    [TestMethod]
    public void TheCampOption_IsAddedFromTheAfterLaunchEvent_OnTheCampSubMenu_NotThePlainLaunchEvent()
    {
        var behavior = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        var option = Hooks("FarWatchCampOption.cs");

        StringAssert.Contains(behavior, "CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus)");
        Assert.IsFalse(behavior.Contains("CampaignEvents.OnSessionLaunchedEvent"),
            "listener order inside OnSessionLaunched is not pinned, so the option must not be added there");
        StringAssert.Contains(option, "FieldCampCampaignBehavior.CampSubMenuId, OptionId");
        StringAssert.Contains(option, "isLeave: false, index: 5");
        StringAssert.Contains(option, "\"taom_fc_scout\"");
    }

    [TestMethod]
    public void TheBehavior_RegistersExactlyTheFourEvents()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        var events = Regex.Matches(src, @"CampaignEvents\.(\w+)\.AddNonSerializedListener").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        CollectionAssert.AreEquivalent(new[] { "OnAfterSessionLaunchedEvent", "HourlyTickEvent", "MapEventStarted", "OnPartyEncounterEvent" }, events);
        StringAssert.Contains(src, "CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick)");
        var tick = src.Substring(src.IndexOf("void OnHourlyTick()", StringComparison.Ordinal));
        Assert.IsTrue(tick.IndexOf("_reconcile.OnHourlyTick();", StringComparison.Ordinal) is var judged && judged >= 0
                      && tick.IndexOf("_option.RefreshWhenStale();", StringComparison.Ordinal) > judged, "the ride is judged before the panel is refreshed");
        StringAssert.Contains(src, "CampaignEvents.MapEventStarted.AddNonSerializedListener(this, _reconcile.OnMapEventStarted)");
        StringAssert.Contains(src, "CampaignEvents.OnPartyEncounterEvent.AddNonSerializedListener(this, _reconcile.OnPartyEncounter)");
        Assert.IsFalse(src.Contains("TickEvent.AddNonSerializedListener(this, On") && src.Contains("CampaignEvents.TickEvent"),
            "TickEvent never fires in a plain menu, and a wait menu has its own tick");
    }

    // ---- The adapter's reads of an army (engine-bound: pinned by source, comment-stripped)

    private static string Adapter() => RepoPaths.ReadSource("Main/Adapters/FarWatchWorldAdapter.cs", stripComments: true);

    [TestMethod]
    public void TheAdapter_TreatsAPartyAsAttachedOnlyWhenItIsAttached_NotWhenItIsMerelyInTheArmy()
    {
        var src = Adapter();

        StringAssert.Contains(src, "IsAttachedToArmy = party.AttachedTo != null");
        Assert.IsFalse(src.Contains("LeaderParty != party"), "an army member that has not merged yet still moves alone and is a row");
    }

    [TestMethod]
    public void TheAdapter_CountsAnArmyLeadersKinds_OverItsOwnRosterAndEveryAttachedPartys()
    {
        var src = Adapter();

        StringAssert.Contains(src, "AddRoster(party.MemberRoster,");
        StringAssert.Contains(src, "var attached = party.AttachedParties;");
        StringAssert.Contains(src, "AddRoster(attached[i].MemberRoster,");
    }

    [TestMethod]
    public void TheAdapter_NamesAPartyAsItsMapPlateDoes()
    {
        var src = Adapter();

        StringAssert.Contains(src, "army.DoesLeaderPartyAndAttachedPartiesContain(party)");
        StringAssert.Contains(src, "party.ArmyName?.ToString(), party.LeaderHero?.Name?.ToString(), party.Name?.ToString()");
        StringAssert.Contains(src, "Name = PlateName(party)");
    }

    // ---- The wait menu

    [TestMethod]
    public void TheWaitMenuCondition_IsTheLiteralTrue()
    {
        // GameMenu.GetMenuOptionConditionsHold ANDs a wait menu's condition with every option's own, the recall included:
        // anything but `args => true` hides the recall the moment it turns false and strands the player (#567).
        var src = Hooks("FarWatchWaitMenu.cs");

        var start = src.IndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the wait menu is no longer registered with AddWaitGameMenu");
        Assert.AreEqual(start, src.LastIndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal), "one wait menu, one registration site");
        var call = src.Substring(start, src.IndexOf(");", start, StringComparison.Ordinal) - start);

        StringAssert.Contains(call, "args => true,");
        StringAssert.Contains(call, "WaitMenuShowProgressAndHoursOption");
        StringAssert.Contains(call, "\"{=!}{TAOM_FWATCH_WAIT}\"");
        Assert.AreEqual(1, Regex.Matches(AllHooks(), @"AddWaitGameMenu\(").Count, "no other wait menu");
    }

    [TestMethod]
    public void TheRecall_IsTheOnlyLeaveOptionOfTheWaitMenu_AndTheBackIsTheOnlyOneOfThePick()
    {
        var wait = Hooks("FarWatchWaitMenu.cs");
        var pick = Hooks("FarWatchPickMenu.cs");

        Assert.AreEqual(1, Regex.Matches(wait, @"isLeave:\s*true").Count);
        Assert.AreEqual(0, Regex.Matches(wait, @"isLeave:\s*false").Count);
        StringAssert.Contains(wait.Substring(wait.IndexOf("\"taom_fc_scout_recall\"", StringComparison.Ordinal)), "isLeave: true, index: 0");
        Assert.AreEqual(1, Regex.Matches(pick, @"isLeave:\s*true").Count);
        StringAssert.Contains(pick.Substring(pick.IndexOf("\"taom_fc_scout_back\"", StringComparison.Ordinal)), "isLeave: true");
    }

    [TestMethod]
    public void EveryExit_GoesThroughTheLanding()
    {
        // ExitToLast and SwitchTo(variable) appear in the landing and nowhere else; the two menus open each other by constant id.
        foreach (var file in new[] { "FarWatchWaitMenu.cs", "FarWatchReconcile.cs", "FarWatchCampOption.cs", "FarWatchPickMenu.cs", "FarWatchPresenter.cs" })
        {
            var src = Hooks(file);
            Assert.IsFalse(Regex.IsMatch(src, @"ExitToLast|\.SwitchTo\(|Finish\("), file + " exits by itself instead of through the landing");
        }

        Assert.AreEqual(1, Regex.Matches(Hooks("FarWatchLanding.cs"), @"ExitToLast\(").Count);
        var land = Hooks("FarWatchLanding.cs");
        Assert.IsTrue(land.IndexOf("GenericStateMenuId", StringComparison.Ordinal) < land.IndexOf("EnsureMenuOpen(", StringComparison.Ordinal),
            "the engine's own choice comes before the camp panel");
        Assert.IsTrue(land.IndexOf("EnsureMenuOpen(", StringComparison.Ordinal) < land.IndexOf("ExitToLast(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheWaitTick_HasItsOwnTryCatchAndNoLambda()
    {
        var wait = Hooks("FarWatchWaitMenu.cs");
        var tick = wait.Substring(wait.IndexOf("internal void OnWaitTick(", StringComparison.Ordinal));
        tick = tick.Substring(0, tick.IndexOf("internal void OnWaitComplete(", StringComparison.Ordinal));

        StringAssert.Contains(tick, "catch (Exception ex)");
        StringAssert.Contains(tick, "Fault(\"wait tick\"");
        Assert.IsFalse(tick.Contains("=>"), "a lambda per frame allocates on the hottest callback");
    }

    [TestMethod]
    public void TheWaitInit_EndsAnActiveWaitFirst_BecauseTheEngineStartsOneOnlyWhenNoneIsActive()
    {
        var wait = Hooks("FarWatchWaitMenu.cs");
        var init = wait.Substring(wait.IndexOf("internal void OnWaitInit(", StringComparison.Ordinal));

        Assert.IsTrue(init.IndexOf("menu.IsWaitActive", StringComparison.Ordinal) < init.IndexOf("menu.EndWait()", StringComparison.Ordinal));
        Assert.IsTrue(init.IndexOf("menu.EndWait()", StringComparison.Ordinal) < init.IndexOf("_service.Progress(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Start_DropsTheRide_WhenTheWaitMenuDoesNotOpen()
    {
        var pick = Hooks("FarWatchPickMenu.cs");
        var start = pick.Substring(pick.IndexOf("internal void Start(", StringComparison.Ordinal));

        var open = start.IndexOf("EnsureMenuOpen(FarWatchWaitMenu.WaitMenuId)", StringComparison.Ordinal);
        var drop = start.IndexOf("_service.End(", open, StringComparison.Ordinal);
        Assert.IsTrue(open >= 0 && drop > open, "a failed open drops the stored ride");
    }

    [TestMethod]
    public void TheListener_ReturnsOnTheStoreBeforeAnythingElse()
    {
        var reconcile = Hooks("FarWatchReconcile.cs");
        var listener = reconcile.Substring(reconcile.IndexOf("internal void OnMapEventStarted(", StringComparison.Ordinal));

        Assert.IsTrue(listener.IndexOf("_service.HasRide", StringComparison.Ordinal) < listener.IndexOf("_world.", StringComparison.Ordinal),
            "the event fires for every battle in the world");
    }

    [TestMethod]
    public void EachHookClass_CatchesAndLogsThroughOneLatch()
    {
        foreach (var file in new[] { "FarWatchWaitMenu.cs", "FarWatchReconcile.cs", "FarWatchPickMenu.cs", "FarWatchCampOption.cs" })
        {
            var src = Hooks(file);
            Assert.IsTrue(src.Contains("_faultLogged"), file + " logs a fault once");
            Assert.IsFalse(Regex.IsMatch(src, @"catch \(Exception inner\)"), file + ": no nested catch around the fault path");
        }
    }

    [TestMethod]
    public void EveryHooksFile_StaysUnder150Lines()
    {
        var files = System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FarWatch", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 8);
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
    public void TheBehavior_SavesTheSixteenPrimitiveKeys_OnceInTheCode_ForBothDirections()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        var keys = new[]
        {
            "_taomFarWatchKind", "_taomFarWatchHero", "_taomFarWatchName", "_taomFarWatchSelf", "_taomFarWatchTarget", "_taomFarWatchTargetName",
            "_taomFarWatchStart", "_taomFarWatchHours", "_taomFarWatchSkill", "_taomFarWatchBuff", "_taomFarWatchBuffStart", "_taomFarWatchBuffHold",
            "_taomFarWatchRest", "_taomFarWatchRestName", "_taomFarWatchNamed", "_taomFarWatchNamedUntil",
        };
        foreach (var key in keys)
            Assert.AreEqual(1, Regex.Matches(src, "\"" + key + "\"").Count, key + ": one SyncData call serves load and save");
        Assert.AreEqual(keys.Length, Regex.Matches(src, @"SyncData\(""_taomFarWatch").Count, "a saved key missing from this list would escape the guard");
        Assert.IsFalse(src.Contains("CampaignTime"), "hours are saved as doubles, with no CampaignTime conversion");
        StringAssert.Contains(src, "new FarWatchRecord()", "a load fills a fresh record, never the live store");
    }
}
