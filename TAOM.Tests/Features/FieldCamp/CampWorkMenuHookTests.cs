using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Activities;
using TAOM.Features.FieldCamp.Activities.Hooks;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The menu classes of camp work (#860) driven through their callbacks with the real service over a plain camp record and a
/// substituted engine. The engine objects a callback is handed are built with <c>GetUninitializedObject</c> where the code reads
/// only fields (the shape of FarWatchMenuHookTests); the wait menu's <c>EndWait</c> branch needs a live campaign and is pinned by
/// source in CampActivitiesWiringTests.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CampWorkMenuHookTests
{
    private ICampService _camps = null!;
    private ICampSettingsProvider _settings = null!;
    private ICampWorkAdapter _work = null!;
    private IGameMenuAdapter _menus = null!;
    private IModLogger _logger = null!;
    private CampState _camp = null!;
    private CampWorkFacts _facts;
    private CampActivityService _service = null!;
    private ScreenFaultLog _faults = null!;

    private static readonly System.Reflection.FieldInfo TicksPerHour =
        typeof(CampaignTime).GetField("TimeTicksPerHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private long _savedTicksPerHour;

    [TestCleanup]
    public void Cleanup() => TicksPerHour.SetValue(null, _savedTicksPerHour);

    [TestInitialize]
    public void SetUp()
    {
        // The engine fills this static when a campaign starts; without it a CampaignTime has no hours (0 ticks over 0).
        _savedTicksPerHour = (long)TicksPerHour.GetValue(null)!;
        TicksPerHour.SetValue(null, 3_600_000L);
        _camp = new CampState { TypeEnum = CampType.Field };
        _camps = Substitute.For<ICampService>();
        _camps.PlayerCamp.Returns(_ => _camp);
        _settings = Substitute.For<ICampSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.CampActivityHours.Returns(10f);
        _settings.CampActivityYieldPerTenMen.Returns(10f);
        _settings.CampActivityYieldCap.Returns(30);
        _facts = new CampWorkFacts { CampReady = true, HealthyMen = 20, HealthyRegulars = 19, ClanTier = 2, ToolsHeld = 5, TerrainType = (int)TerrainType.Forest };
        _work = Substitute.For<ICampWorkAdapter>();
        _work.Read().Returns(_ => _facts);
        _work.WaterNearby().Returns(true);
        _work.AddGoods(Arg.Any<CampActivity>(), Arg.Any<int>()).Returns(c => c.ArgAt<int>(1));
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        _logger = Substitute.For<IModLogger>();
        _service = new CampActivityService(_camps, _settings, _work, coop, Substitute.For<IDedicatedServerProvider>(), _logger);
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _faults = new ScreenFaultLog(_logger, "FieldCamp");
    }

    private void Working(CampActivity activity, float done = 0f, float target = 10f)
    {
        _camp.Activity = (int)activity;
        _camp.ActivityHoursDone = done;
        _camp.ActivityTargetHours = target;
    }

    private static MenuCallbackArgs Args() => new((MenuContext)null!, new TextObject("{=!}x"));

    private static MenuCallbackArgs WaitArgs(out GameMenu menu)
    {
        menu = (GameMenu)FormatterServices.GetUninitializedObject(typeof(GameMenu));
        var context = (MenuContext)FormatterServices.GetUninitializedObject(typeof(MenuContext));
        typeof(MenuContext).GetProperty("GameMenu")!.GetSetMethod(true)!.Invoke(context, new object[] { menu });
        return new MenuCallbackArgs(context, new TextObject("{=!}x"));
    }

    private static string Variable(string name) => new TextObject("{=!}{" + name + "}").ToString();

    private CampWorkWaitMenu Wait() => new(_service, _work, new CampWorkLanding(_menus), _faults);

    private CampWorkMenu Pick() => new(_service, _work, _menus, _logger, _faults);

    // ---- The wait: init

    [TestMethod]
    public void Wait_OnInit_RestoresTheTargetHoursAndTheSavedFraction_AndTheText()
    {
        Working(CampActivity.Timber, done: 2.5f, target: 10f);
        var args = WaitArgs(out var menu);

        Wait().OnWaitInit(args);

        Assert.AreEqual(0.25f, menu.Progress, 1e-5f);
        Assert.AreEqual(10f, menu.TargetWaitHours, 1e-5f);
        Assert.AreEqual("The men are felling timber for the camp.", Variable("TAOM_CAMPWORK_WAIT"));
        _menus.DidNotReceiveWithAnyArgs().ExitToLast();
    }

    [TestMethod]
    public void Wait_OnInit_NoWork_LandsOnTheMap_NeverTheCampPanel()
    {
        Wait().OnWaitInit(WaitArgs(out _));

        _menus.Received(1).ExitToLast();
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Wait_OnInit_ALoadMidWork_ReadsTheSavedHours_NotAClock()
    {
        Working(CampActivity.Hunt, done: 9f, target: 10f);
        var args = WaitArgs(out var menu);

        Wait().OnWaitInit(args);

        Assert.AreEqual(0.9f, menu.Progress, 1e-5f);
    }

    // ---- The wait: tick

    [TestMethod]
    public void Wait_OnTick_AddsTheElapsedHours_AndWritesTheProgress()
    {
        Working(CampActivity.Hunt, done: 1f, target: 10f);
        var args = WaitArgs(out var menu);

        Wait().OnWaitTick(args, CampaignTime.Hours(1.5f));

        Assert.AreEqual(2.5f, _camp.ActivityHoursDone, 1e-4f);
        Assert.AreEqual(0.25f, menu.Progress, 1e-4f);
        _menus.DidNotReceiveWithAnyArgs().ExitToLast();
    }

    [TestMethod]
    public void Wait_OnTick_TheWorkIsGone_Lands()
    {
        Wait().OnWaitTick(WaitArgs(out _), default);

        _menus.Received(1).ExitToLast();
    }

    [TestMethod]
    public void Wait_OnTick_TheCampWasBroken_Lands()
    {
        Working(CampActivity.Hunt, done: 1f);
        _camp = null!;

        Wait().OnWaitTick(WaitArgs(out _), default);

        _menus.Received(1).ExitToLast();
    }

    [TestMethod]
    public void Wait_OnTick_ACrossingOfTheMidpoint_WoundsAndTellsThePlayer()
    {
        Working(CampActivity.Hunt, done: 4.9f);
        _work.NextRandom().Returns(0f);
        _work.WoundRegulars(Arg.Any<int>()).Returns(1);

        Wait().OnWaitTick(WaitArgs(out _), CampaignTime.Hours(0.5f));

        _work.Received(1).ShowMessage("A man was hurt at the work.");
    }

    [TestMethod]
    public void Wait_OnTick_AFault_LogsOnce_ButEveryFaultDropsTheWorkAndLands()
    {
        var sut = Wait();
        Working(CampActivity.Hunt, done: 1f);

        // A tick handed no menu context throws inside the guard, after the hours were added.
        sut.OnWaitTick(Args(), CampaignTime.Hours(1f));
        Assert.AreEqual(0, _camp.Activity, "the fault dropped the work");
        Working(CampActivity.Hunt, done: 1f);
        sut.OnWaitTick(Args(), CampaignTime.Hours(1f));

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.AreEqual(0, _camp.Activity, "the second fault drops and lands too: the once-only latch gates the log line alone");
        _menus.Received(2).ExitToLast();
    }

    // ---- The wait: completion and stop

    [TestMethod]
    public void Wait_OnComplete_PaysLandsOnTheMap_AndPrintsTheLines()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 25;

        Wait().OnWaitComplete(Args());

        _work.Received(1).AddGoods(CampActivity.Hunt, 25);
        _menus.Received(1).ExitToLast();
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _work.Received(1).ShowMessage("The hunters return with 25 meat.");
        _work.Received(1).ShowMessage("The long work wears on the men (morale -0.8).");
        _work.Received(1).ShowMessage("3 tools are worn out.");
    }

    [TestMethod]
    public void Wait_OnComplete_OneToolWornOut_IsNotOneTools()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 40;
        _facts.ToolsHeld = 1;

        Wait().OnWaitComplete(Args());

        _work.Received(1).ShowMessage("A tool is worn out.");
        _work.DidNotReceive().ShowMessage("1 tools are worn out.");
    }

    [TestMethod]
    public void Wait_OnTick_ASharedCampaign_EndsTheWorkWithOneMessage_AndLands()
    {
        Working(CampActivity.Hunt, done: 1f);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsSessionActive.Returns(true);
        var service = new CampActivityService(_camps, _settings, _work, coop, Substitute.For<IDedicatedServerProvider>(), _logger);
        var sut = new CampWorkWaitMenu(service, _work, new CampWorkLanding(_menus), _faults);

        sut.OnWaitTick(WaitArgs(out _), CampaignTime.Hours(1f));
        sut.OnWaitTick(WaitArgs(out _), CampaignTime.Hours(1f));

        Assert.AreEqual(0, _camp.Activity);
        _work.Received(1).ShowMessage("Camp work is not available in a shared campaign. The men stand down.");
        _menus.Received(2).ExitToLast();
    }

    [TestMethod]
    public void Wait_OnInit_TheMasterToggleOff_EndsTheWorkAndLands()
    {
        Working(CampActivity.Hunt, done: 2f);
        _settings.Enabled.Returns(false);

        Wait().OnWaitInit(WaitArgs(out _));

        Assert.AreEqual(0, _camp.Activity);
        _menus.Received(1).ExitToLast();
    }

    [TestMethod]
    public void Wait_OnComplete_NothingBroughtIn_SaysSo()
    {
        Working(CampActivity.Hunt, done: 10f);
        _settings.CampActivityYieldPerTenMen.Returns(0f);
        _facts.HealthyMen = 10;

        Wait().OnWaitComplete(Args());

        _work.Received(1).ShowMessage("The men return empty-handed.");
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
    }

    [TestMethod]
    public void Wait_OnComplete_AFault_IsReported_StillLands_AndDoesNotPayTwice()
    {
        Working(CampActivity.Hunt, done: 10f);
        _work.AddGoods(Arg.Any<CampActivity>(), Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("roster write"));
        var sut = Wait();

        sut.OnWaitComplete(Args());
        sut.OnWaitComplete(Args());

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        _menus.Received(2).ExitToLast();
        _work.Received(1).AddGoods(Arg.Any<CampActivity>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Stop_DiscardsTheProgress_Lands_AndPaysNothing()
    {
        Working(CampActivity.Hunt, done: 9f);

        Wait().OnStop();

        Assert.AreEqual(0, _camp.Activity);
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
        _menus.Received(1).ExitToLast();
        _work.Received(1).ShowMessage("The men stand down. The work is abandoned.");
    }

    // ---- The landing

    [TestMethod]
    public void Land_TheEngineNamesAMenu_GoesThere()
    {
        _menus.GenericStateMenuId.Returns("encounter");
        _menus.SwitchTo("encounter").Returns(true);

        new CampWorkLanding(_menus).Land();

        _menus.Received(1).SwitchTo("encounter");
        _menus.DidNotReceive().ExitToLast();
    }

    [DataTestMethod]
    [DataRow(CampWorkWaitMenu.WaitMenuId)]
    [DataRow(CampWorkMenu.PickMenuId)]
    public void Land_TheEngineNamesTheWorksOwnMenu_IsNotALanding(string own)
    {
        _menus.GenericStateMenuId.Returns(own);

        new CampWorkLanding(_menus).Land();

        _menus.DidNotReceive().SwitchTo(Arg.Any<string>());
        _menus.Received(1).ExitToLast();
    }

    [TestMethod]
    public void Land_TheEngineMenuWillNotOpen_ExitsToTheMap()
    {
        _menus.GenericStateMenuId.Returns("encounter");
        _menus.SwitchTo("encounter").Returns(false);

        new CampWorkLanding(_menus).Land();

        _menus.Received(1).ExitToLast();
    }

    // ---- The camp option

    [TestMethod]
    public void Option_FeatureOff_NoWork_IsHidden()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(Pick().OptionCondition(Args()));
    }

    [TestMethod]
    public void Option_FeatureOff_ARunningWork_IsHiddenToo_TheWorkEndsAtItsNextCall()
    {
        _settings.Enabled.Returns(false);
        Working(CampActivity.Hunt);

        Assert.IsFalse(Pick().OptionCondition(Args()));
    }

    [TestMethod]
    public void Option_ASavedWork_OnAReadyCamp_ReadsReturnToTheWork_Enabled()
    {
        Working(CampActivity.Hunt);
        var args = Args();

        Assert.IsTrue(Pick().OptionCondition(args));

        Assert.IsTrue(args.IsEnabled);
        Assert.AreEqual("Return to the work", Variable("TAOM_CAMPWORK_OPTION"));
    }

    [TestMethod]
    public void Option_ASavedWork_WhileTheCampIsRaisedAgain_IsGreyedWithAReason()
    {
        // Fortify restarts the raise: the saved work must not resume under a half-built palisade.
        Working(CampActivity.Hunt, done: 3f);
        _facts.CampReady = false;
        var args = Args();

        Assert.IsTrue(Pick().OptionCondition(args));

        Assert.IsFalse(args.IsEnabled);
        Assert.AreEqual("Raise a field or fortified camp first.", args.Tooltip.ToString());
    }

    [TestMethod]
    public void Option_ASavedWork_WhileAScoutRideIsOut_IsGreyedWithAReason()
    {
        Working(CampActivity.Hunt, done: 3f);
        _facts.ScoutRideOut = true;
        var args = Args();

        Assert.IsTrue(Pick().OptionCondition(args));

        Assert.IsFalse(args.IsEnabled);
        Assert.AreEqual("The men cannot work while a scout is out.", args.Tooltip.ToString());
    }

    [TestMethod]
    public void Option_ASavedWork_DuringAnEncounter_IsGreyed()
    {
        Working(CampActivity.Hunt, done: 3f);
        _facts.Busy = true;
        var args = Args();

        Pick().OptionCondition(args);

        Assert.IsFalse(args.IsEnabled);
    }

    [TestMethod]
    public void Option_Ready_ShowsTheLabel_Enabled()
    {
        var args = Args();

        Assert.IsTrue(Pick().OptionCondition(args));

        Assert.IsTrue(args.IsEnabled);
        Assert.AreEqual("Set the men to work", Variable("TAOM_CAMPWORK_OPTION"));
    }

    [TestMethod]
    public void Option_ACampNotReady_IsShownGreyed_WithAReason()
    {
        _facts.CampReady = false;
        var args = Args();

        Assert.IsTrue(Pick().OptionCondition(args));

        Assert.IsFalse(args.IsEnabled);
        Assert.AreEqual("Raise a field or fortified camp first.", args.Tooltip.ToString());
    }

    [TestMethod]
    public void Option_AFaultInTheCondition_LogsOnce_AndHidesTheOption()
    {
        _work.Read().Returns(_ => throw new InvalidOperationException("no campaign"));
        var sut = Pick();

        Assert.IsFalse(sut.OptionCondition(Args()));
        Assert.IsFalse(sut.OptionCondition(Args()));

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void OptionConsequence_NoWork_OpensThePick()
    {
        Pick().OptionConsequence(Args());

        _menus.Received(1).EnsureMenuOpen(CampWorkMenu.PickMenuId);
    }

    [TestMethod]
    public void OptionConsequence_ARunningWork_OpensTheWaitMenuDirectly()
    {
        Working(CampActivity.Hunt);

        Pick().OptionConsequence(Args());

        _menus.Received(1).EnsureMenuOpen(CampWorkWaitMenu.WaitMenuId);
    }

    [TestMethod]
    public void OptionConsequence_AMenuThatWillNotOpen_WarnsAndStaysPut()
    {
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(false);

        Pick().OptionConsequence(Args());

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    // ---- The pick

    [TestMethod]
    public void Pick_Start_SavesTheWork_AndOpensTheWaitMenu()
    {
        Pick().Start(CampActivity.Fish);

        Assert.AreEqual((int)CampActivity.Fish, _camp.Activity);
        _menus.Received(1).EnsureMenuOpen(CampWorkWaitMenu.WaitMenuId);
    }

    [TestMethod]
    public void Pick_TheWaitMenuWillNotOpen_DropsTheWork_AndWarns()
    {
        _menus.EnsureMenuOpen(CampWorkWaitMenu.WaitMenuId).Returns(false);

        Pick().Start(CampActivity.Hunt);

        Assert.AreEqual(0, _camp.Activity, "nothing runs behind a menu the player cannot see");
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void Pick_Blocked_ShowsTheReason_AndStartsNothing()
    {
        _facts.TerrainType = (int)TerrainType.Plain;

        Pick().Start(CampActivity.Timber);

        Assert.AreEqual(0, _camp.Activity);
        _work.Received(1).ShowMessage("Only a forest has timber worth felling.");
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Pick_AFaultAfterTheStart_DropsTheWork()
    {
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("menu"));

        Pick().Start(CampActivity.Hunt);

        Assert.AreEqual(0, _camp.Activity);
        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Pick_AGreyedChoice_SaysWhy()
    {
        _facts.HealthyMen = 40;
        _facts.ToolsHeld = 1;
        var args = Args();

        Assert.IsTrue(Pick().ChoiceCondition(args, CampActivity.Hunt));

        Assert.IsFalse(args.IsEnabled);
        Assert.AreEqual("This many men need 4 tools, and the party holds too few.", args.Tooltip.ToString());
    }

    [TestMethod]
    public void Pick_ARunningWork_GreysEveryChoice_WithTheAlreadyAtWorkReason()
    {
        Working(CampActivity.Hunt);
        var sut = Pick();
        var args = Args();

        sut.ChoiceCondition(args, CampActivity.Timber);

        Assert.IsFalse(args.IsEnabled);
        Assert.AreEqual("The men are already at work.", args.Tooltip.ToString());
    }

    [TestMethod]
    public void StartBlocked_NamesTheReasonInPlainWords()
    {
        StringAssert.Contains(CampRestStartTexts.StartBlocked(CampBlockReason.InSettlement), "the party stands inside a settlement");
        StringAssert.Contains(CampRestStartTexts.StartBlocked(CampBlockReason.Moving), "the party is on the move");
        StringAssert.Contains(CampRestStartTexts.StartBlocked(CampBlockReason.External), "something nearby forbids a camp here");
        StringAssert.Contains(CampRestStartTexts.StartBlocked(CampBlockReason.Enlisted), "a camp cannot be pitched here");
    }

    [TestMethod]
    public void RestStatus_NamesTheTwoRatesWithTheirUnits()
    {
        Assert.AreEqual("Resting here heals 15 more wounded troops a day, and the heroes 19 more hit points a day.",
            CampRestStartTexts.RestStatus(15f, 19f).ToString());
    }
}
