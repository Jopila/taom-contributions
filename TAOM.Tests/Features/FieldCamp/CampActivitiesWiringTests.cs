using System;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.FieldCamp.Activities;
using TAOM.Features.FieldCamp.Activities.Hooks;
using TAOM.Features.HostReview;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Wiring guard for camp work (#860): the module line and its place after the other camp-panel options, the container graph, the
/// per-campaign behavior, the camp option's index, and the menu rules that bit the field camp (#567: a false wait-menu condition
/// hides every option; only an isLeave option gets EndWait) plus the fault rule (the log latch never blocks the drop and the
/// landing). The callbacks need a running campaign, so the menu pins read the Hooks sources, comment-stripped.
/// </summary>
[TestClass]
public class CampActivitiesWiringTests
{
    private const string ModulePath = "Main/Features/FieldCamp/Activities/CampActivitiesModule.cs";
    private const string BehaviorPath = "Main/Features/FieldCamp/Activities/Hooks/CampActivitiesBehavior.cs";

    private static readonly Regex InlinePerCampaign = new(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+CampActivitiesBehavior\(");
    private static readonly Regex AnyConstruction = new(@"new\s+CampActivitiesBehavior\(");

    private static string Hooks(string file) =>
        RepoPaths.ReadSource("Main/Features/FieldCamp/Activities/Hooks/" + file, stripComments: true);

    private static string AllHooks() => string.Concat(
        System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FieldCamp", "Activities", "Hooks"), "*.cs")
            .Select(path => Hooks(System.IO.Path.GetFileName(path))));

    [TestCleanup]
    public void Cleanup() => FieldCampMenuController.RestStatus = null;

    private static Container NewContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICampSettingsProvider>());
        container.RegisterInstance(Substitute.For<ICampService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IMapTerrainAdapter>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.Register<FarWatchStore>(Reuse.Singleton);
        new CampActivitiesModule().RegisterServices(container);
        return container;
    }

    // ---- The module

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce_AfterEveryOtherCampPanelOption()
    {
        var all = FeatureModules.All;
        Assert.AreEqual(1, all.OfType<CampActivitiesModule>().Count(), "listed exactly once in Main/Composition/FeatureModules.cs");
        int work = Array.FindIndex(all, m => m is CampActivitiesModule);
        Assert.IsTrue(work > Array.FindIndex(all, m => m is FarWatchModule), "the later listener inserts first, so it ends below Far Watch's");
        Assert.IsTrue(work > Array.FindIndex(all, m => m is HostReviewModule));
    }

    [TestMethod]
    public void TheModule_IsNamed_NotParked_OwnsNoSaveData_DeclaresOneBehavior_AndNoPatches()
    {
        var module = new CampActivitiesModule();

        Assert.AreEqual("CampActivities", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the work is a field of the camp record the Field Camp behavior saves");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(CampActivitiesBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody_ExactlyOnce()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.AreEqual(1, InlinePerCampaign.Matches(src).Count);
        Assert.AreEqual(1, AnyConstruction.Matches(src).Count);
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !path.EndsWith("CampActivitiesModule.cs") && !path.EndsWith("CampActivitiesBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "CampActivitiesBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void IoC_DoesNotMentionTheFeature_AndSubModulePassesOnlyTheRestHealingToTheModel()
    {
        var ioc = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var sub = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(ioc.Contains("CampActivit") || ioc.Contains("CampWork") || ioc.Contains("CampRestHealing") || ioc.Contains("CampStartInCamp"),
            "the module registers everything; a hand registration doubles it");
        Assert.IsFalse(sub.Contains("CampActivit") || sub.Contains("CampWork") || sub.Contains("CampStartInCamp"));
        Assert.AreEqual(1, Regex.Matches(sub, @"CampRestHealing").Count, "the one resolve SubModule.cs needs for the model's constructor");
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = NewContainer();

        Assert.IsNotNull(container.Resolve<CampActivityService>());
        Assert.AreSame(container.Resolve<CampActivityService>(), container.Resolve<CampActivityService>());
        Assert.IsInstanceOfType(container.Resolve<ICampWorkAdapter>(), typeof(CampWorkAdapter));
        Assert.IsNotNull(container.Resolve<CampRestHealing>());
        Assert.AreSame(container.Resolve<CampRestHealing>(), container.Resolve<CampRestHealing>());
    }

    [TestMethod]
    public void InitializeStatics_GivesTheCampPanelTheContainersRestHealing()
    {
        using var container = NewContainer();

        new CampActivitiesModule().InitializeStatics(container);

        Assert.AreSame(container.Resolve<CampRestHealing>(), FieldCampMenuController.RestStatus);
    }

    // ---- The behavior

    [TestMethod]
    public void TheBehavior_AddsItsMenusFromTheAfterLaunchEvent_AndRegistersOnlyTheStartInCampEventsBesides()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        var events = Regex.Matches(src, @"CampaignEvents\.(\w+)\.AddNonSerializedListener").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        CollectionAssert.AreEqual(new[] { "OnAfterSessionLaunchedEvent", "OnCharacterCreationIsOverEvent", "TickEvent" }, events,
            "the tick belongs to start-in-camp alone (a menu has its own tick), and there is no load handler (a throw there loops the loading screen)");
        Assert.IsFalse(src.Contains("OnGameLoaded"));
    }

    [TestMethod]
    public void TheBehavior_SavesNothingItself_TheWorkRidesInTheCampBook()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        var fieldCamp = RepoPaths.ReadSource("Main/Features/FieldCamp/Hooks/FieldCampCampaignBehavior.cs", stripComments: true);

        Assert.IsFalse(src.Contains("dataStore.SyncData"));
        Assert.AreEqual(2, Regex.Matches(fieldCamp, "\"_taomFieldCamps\"").Count, "one key, both directions, and no second key for the work");
    }

    [TestMethod]
    public void TheCampOption_IsOnTheCampSubMenu_AtIndexFive_AddedFromTheBehavior()
    {
        var option = Hooks("CampWorkMenu.cs");

        StringAssert.Contains(option, "FieldCampCampaignBehavior.CampSubMenuId, OptionId");
        StringAssert.Contains(option, "isLeave: false, index: 5");
        StringAssert.Contains(RepoPaths.ReadSource(BehaviorPath, stripComments: true), "_pick.AddOption(starter)");
    }

    // ---- The wait menu

    [TestMethod]
    public void ThePickMenu_IsRegisteredWithNoInitDelegate_AndKeyedLiterals()
    {
        var pick = Hooks("CampWorkMenu.cs");

        StringAssert.Contains(pick, "\"{=taom_camp_pick_body}");
        StringAssert.Contains(pick, "\"{=taom_camp_opt_hunt}");
        StringAssert.Contains(pick, "\"{=taom_camp_opt_timber}");
        StringAssert.Contains(pick, "\"{=taom_camp_opt_fish}");
        Assert.IsFalse(pick.Contains("OnPickInit") || pick.Contains("TAOM_CAMPWORK_PICK") || pick.Contains("TAOM_CAMPWORK_CHOICE"),
            "the body and the labels are registered text; there is no init to set variables");
        Assert.IsFalse(Regex.IsMatch(pick, @"AddGameMenu\(PickMenuId,[^;]*OnInitDelegate\("), "an init that only set text variables has no work left");
    }

    [TestMethod]
    public void TheCampPanelsStatusLine_IsDrawnFromTheControllersRestHealingStatic_OnTheCampPanelOnly()
    {
        var controller = RepoPaths.ReadSource("Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs", stripComments: true);
        var behavior = RepoPaths.ReadSource("Main/Features/FieldCamp/Hooks/FieldCampCampaignBehavior.cs", stripComments: true);

        StringAssert.Contains(controller, "internal static CampRestHealing? RestStatus");
        StringAssert.Contains(controller, "RestStatus?.StatusRates()");
        Assert.AreEqual(1, Regex.Matches(controller, @"onCampPanel:\s*false").Count, "the base menu registration");
        Assert.AreEqual(1, Regex.Matches(controller, @"new OnInitDelegate\(args => RefreshStatusVariables\(onCampPanel: true\)\)").Count, "the camp panel registration");
        Assert.IsFalse(controller.Contains("IsCampPanel") || controller.Contains("OnMenuInit"));
        Assert.IsFalse(behavior.Contains("RestStatus"), "the behavior keeps its HEAD shape; the controller is the only reader");
        Assert.IsNull(typeof(ICampService).Assembly.GetType("TAOM.Features.FieldCamp.ICampRestStatus"), "no interface for one implementation");
    }

    [TestMethod]
    public void TheWaitMenuCondition_IsTheLiteralTrue()
    {
        var src = Hooks("CampWorkWaitMenu.cs");

        var start = src.IndexOf("starter.AddWaitGameMenu(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0);
        var call = src.Substring(start, src.IndexOf(");", start, StringComparison.Ordinal) - start);

        StringAssert.Contains(call, "args => true,");
        StringAssert.Contains(call, "WaitMenuShowProgressAndHoursOption");
        Assert.AreEqual(1, Regex.Matches(AllHooks(), @"AddWaitGameMenu\(").Count, "no other wait menu");
    }

    [TestMethod]
    public void Stop_IsTheOnlyLeaveOptionOfTheWaitMenu_AndBackTheOnlyOneOfThePick()
    {
        var wait = Hooks("CampWorkWaitMenu.cs");
        var pick = Hooks("CampWorkMenu.cs");

        Assert.AreEqual(1, Regex.Matches(wait, @"isLeave:\s*true").Count);
        Assert.AreEqual(0, Regex.Matches(wait, @"isLeave:\s*false").Count);
        Assert.AreEqual(1, Regex.Matches(pick, @"isLeave:\s*true").Count);
        StringAssert.Contains(pick.Substring(pick.IndexOf("\"taom_fc_work_back\"", StringComparison.Ordinal)), "isLeave: true");
    }

    [TestMethod]
    public void EveryExit_GoesThroughTheLanding_AndTheLandingNeverOpensTheCampPanel()
    {
        foreach (var file in new[] { "CampWorkWaitMenu.cs", "CampWorkMenu.cs", "CampActivitiesBehavior.cs", "CampWorkTexts.cs" })
            Assert.IsFalse(Regex.IsMatch(Hooks(file), @"ExitToLast|\.SwitchTo\(|Finish\("), file + " exits by itself instead of through the landing");

        var land = Hooks("CampWorkLanding.cs");
        Assert.AreEqual(1, Regex.Matches(land, @"ExitToLast\(").Count);
        Assert.IsTrue(land.IndexOf("GenericStateMenuId", StringComparison.Ordinal) < land.IndexOf("ExitToLast(", StringComparison.Ordinal),
            "the engine's own choice comes before the map");
        Assert.IsFalse(land.Contains("CampSubMenuId") || land.Contains("EnsureMenuOpen"), "after timed work the player lands on the map, never the camp panel");
    }

    [TestMethod]
    public void TheWaitTick_HasItsOwnTryCatchAndNoLambda()
    {
        var wait = Hooks("CampWorkWaitMenu.cs");
        var tick = wait.Substring(wait.IndexOf("internal void OnWaitTick(", StringComparison.Ordinal));
        tick = tick.Substring(0, tick.IndexOf("internal void OnWaitComplete(", StringComparison.Ordinal));

        StringAssert.Contains(tick, "catch (Exception ex)");
        StringAssert.Contains(tick, "Fault(\"wait tick\"");
        Assert.IsFalse(tick.Contains("=>"), "a lambda per frame allocates on the hottest callback");
    }

    [TestMethod]
    public void TheWaitInit_EndsAnActiveWaitFirst_BecauseTheEngineStartsOneOnlyWhenNoneIsActive()
    {
        var wait = Hooks("CampWorkWaitMenu.cs");
        var init = wait.Substring(wait.IndexOf("internal void OnWaitInit(", StringComparison.Ordinal));

        Assert.IsTrue(init.IndexOf("menu.IsWaitActive", StringComparison.Ordinal) < init.IndexOf("menu.EndWait()", StringComparison.Ordinal));
        Assert.IsTrue(init.IndexOf("menu.EndWait()", StringComparison.Ordinal) < init.IndexOf("_service.Current()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EveryHooksFile_StaysUnder150Lines()
    {
        var files = System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FieldCamp", "Activities", "Hooks"), "*.cs");
        Assert.IsTrue(files.Length >= 5);
        foreach (var file in files)
            Assert.IsTrue(System.IO.File.ReadAllLines(file).Length < 150, "ADR-002: " + System.IO.Path.GetFileName(file) + " is 150 lines or more");
    }

    // ---- The adapter (engine-bound: pinned by source, comment-stripped)

    private static string Adapter() => RepoPaths.ReadSource("Main/Features/FieldCamp/Activities/CampWorkAdapter.cs", stripComments: true);

    [TestMethod]
    public void TheAdapter_HoldsOnlyItsThreeCollaborators_NoCachedEngineObject()
    {
        var fields = typeof(CampWorkAdapter).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        CollectionAssert.AreEquivalent(new[] { "_camps", "_terrain", "_scoutRide" }, fields.Select(f => f.Name).ToArray());
    }

    [TestMethod]
    public void TheAdapter_ReadsTheMainPartyNullSafe_NeverThroughTheThrowingStatic()
    {
        var src = Adapter();

        Assert.IsFalse(src.Contains("MobileParty.MainParty"), "MobileParty.MainParty dereferences Campaign.Current; the adapter's doc promises no throw without a campaign");
        StringAssert.Contains(src, "Campaign.Current?.MainParty");
    }

    [TestMethod]
    public void TheAdapter_SamplesWaterAtEightPointsAndTwoDistances_ThroughTheSafeTerrainRead()
    {
        var src = Adapter();

        StringAssert.Contains(src, "WaterSampleDistances = { 2f, 4f }");
        StringAssert.Contains(src, "CompassPoints = 8");
        StringAssert.Contains(src, "_terrain.TerrainTypeAt(");
        Assert.IsFalse(src.Contains("GetTerrainTypeAtPosition") || src.Contains("GetEnvironmentTerrainTypes"), "not the engine's drifting sampler");
    }

    [TestMethod]
    public void TheAdapter_WoundsHealthyRegularsThroughTheEnginesDraw_ClampedToThem_AndLooksFishUpById()
    {
        var src = Adapter();

        // The engine's draw is even over the healthy regulars and never lands on a hero; the clamp is the healthy-regular count.
        StringAssert.Contains(src, "roster.WoundNumberOfNonHeroTroopsRandomly(");
        StringAssert.Contains(src, "Math.Min(count, roster.TotalRegulars - roster.TotalWoundedRegulars)");
        Assert.IsFalse(src.Contains("AddToCountsAtIndex"), "no hand-rolled walk over the roster");
        StringAssert.Contains(src, "GetObject<ItemObject>(\"fish\")");
        StringAssert.Contains(src, "DefaultItems.Meat");
        StringAssert.Contains(src, "DefaultItems.HardWood");
        StringAssert.Contains(src, "DefaultItems.Tools");
    }

    // ---- Text

    [TestMethod]
    public void ThePickMenu_HasNoResumeOption_TheCampPanelOptionIsTheOnlyWayBack()
    {
        var pick = Hooks("CampWorkMenu.cs");

        Assert.IsFalse(pick.Contains("taom_fc_work_resume") || pick.Contains("taom_camp_opt_resume}"));
        var texts = RepoPaths.ReadSource("Main/Features/FieldCamp/Activities/Hooks/CampWorkTexts.cs", stripComments: false);
        Assert.IsFalse(texts.Contains("Resume the work"));
    }

    [TestMethod]
    public void NoStringFileCarriesTheDeletedKeys()
    {
        var dir = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        // One scan: the core file and the language files all sit under ModuleData.
        var deleted = new[] { "taom_camp_opt_resume", "taom_camp_done_tools", "taom_camp_start_close", "taom_camp_status_with_rest" };
        foreach (var file in System.IO.Directory.GetFiles(dir, "*.xml", System.IO.SearchOption.AllDirectories))
        {
            var text = System.IO.File.ReadAllText(file);
            foreach (var key in deleted)
                Assert.IsFalse(text.Contains("\"" + key + "\""), key + " in " + file);
        }
    }

    [TestMethod]
    public void TheTexts_AreAllTaomCampKeys_WithNoEmOrEnDash()
    {
        var raw = string.Concat(new[] { "CampWorkTexts.cs", "CampRestStartTexts.cs", "CampWorkMenu.cs", "CampWorkWaitMenu.cs" }
            .Select(f => RepoPaths.ReadSource("Main/Features/FieldCamp/Activities/Hooks/" + f, stripComments: false)));

        var keys = Regex.Matches(raw, @"\{=(\w+)\}").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.IsTrue(keys.Count >= 33);
        Assert.IsTrue(keys.All(k => k.StartsWith("taom_camp_", StringComparison.Ordinal)), string.Join(", ", keys.Where(k => !k.StartsWith("taom_camp_"))));
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), "one key per text");
        Assert.IsFalse(raw.Contains('—') || raw.Contains('–'));
    }

    [TestMethod]
    public void TheCoopDoc_HasARowForTheBehavior()
    {
        var doc = RepoPaths.ReadSource("docs/features/coop-interop.md", stripComments: false);

        StringAssert.Contains(doc, "| `CampActivitiesBehavior` |");
    }
}
