using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The three menu classes of Far Watch, driven through their callbacks with the real service over the real store and a
/// substituted engine (the shape of Host Review's lesson: a hook that "needs the game" is pinned by what it decides, and the
/// engine objects it is handed are built with <c>GetUninitializedObject</c> where the code reads only fields). The wait menu's
/// <c>EndWait</c> branch needs a live campaign and is pinned by source in FarWatchWiringTests.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchMenuHookTests
{
    private const double Now = 1000d;

    private FarWatchStore _store = null!;
    private IFarWatchWorldAdapter _world = null!;
    private IGameMenuAdapter _menus = null!;
    private IFarWatchNotifier _notifier = null!;
    private IModLogger _logger = null!;
    private ICampSettingsProvider _camps = null!;
    private FarWatchService _service = null!;
    private FarWatchLanding _landing = null!;
    private FarWatchPresenter _presenter = null!;

    [TestCleanup]
    public void Cleanup() => FarWatchTexts.HoursText = FarWatchTexts.DefaultHoursText;

    [TestInitialize]
    public void SetUp()
    {
        FarWatchTexts.HoursText = hours => new TextObject("{=!}{H} hours").SetTextVariable("H", hours);
        _logger = Substitute.For<IModLogger>();
        _store = new FarWatchStore(_logger);
        _world = Substitute.For<IFarWatchWorldAdapter>();
        _world.NowHours.Returns(Now);
        _world.ReadCamp().Returns(Camp());
        _world.IsHeroWithParty("scout_1").Returns(true);
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _notifier = Substitute.For<IFarWatchNotifier>();
        _camps = Substitute.For<ICampSettingsProvider>();
        _camps.Enabled.Returns(true);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        _service = new FarWatchService(_store, _world, new HiddenHostsService(Substitute.For<IHiddenHostsSettings>(), _store), coop,
            Substitute.For<IDedicatedServerProvider>(), _camps, _logger);
        _landing = new FarWatchLanding(_menus, _world, _logger);
        _presenter = new FarWatchPresenter(_notifier);
    }

    private static ScoutCampFacts Camp() => new()
    {
        CampExists = true, CampReady = true, MainHeroId = "main", MainHeroName = "Aldric",
        AssignedScoutId = "scout_1", AssignedScoutName = "Mirien", AssignedScoutSkill = 150, AssignedScoutInParty = true,
    };

    private static MenuCallbackArgs Args() => new((MenuContext)null!, new TextObject("{=!}x"));

    private static string Variable(string name) => new TextObject("{=!}{" + name + "}").ToString();

    private void Ride(double start = Now - 1d) =>
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, start, 6d, 150);

    // ---- The camp option

    private FarWatchCampOption Option() => new(_service, _world, _menus, _logger);

    [TestMethod]
    public void Option_Ready_ShowsSendAScout_AsASubmenu_Enabled()
    {
        var args = Args();

        Assert.IsTrue(Option().Condition(args));

        Assert.IsTrue(args.IsEnabled);
        Assert.AreEqual(GameMenuOption.LeaveType.Submenu, args.optionLeaveType);
        Assert.AreEqual("Send a scout", Variable("TAOM_FWATCH_OPTION"));
    }

    [TestMethod]
    public void Option_TheHeroAlone_ReadsRideOutToScout()
    {
        _world.ReadCamp().Returns(Camp() with { PartyAlone = true, AssignedScoutId = null, AssignedScoutInParty = false });

        Option().Condition(Args());

        Assert.AreEqual("Ride out to scout", Variable("TAOM_FWATCH_OPTION"));
    }

    [TestMethod]
    public void Option_ARideRuns_ReadsAwaitYourScout_AsAWaitOption_Enabled()
    {
        Ride();
        var args = Args();

        Assert.IsTrue(Option().Condition(args));

        Assert.IsTrue(args.IsEnabled);
        Assert.AreEqual(GameMenuOption.LeaveType.Wait, args.optionLeaveType);
        Assert.AreEqual("Await your scout", Variable("TAOM_FWATCH_OPTION"));
    }

    [TestMethod]
    public void Option_ASoloRideRuns_ReadsContinueTheRide()
    {
        _store.StartRide(ScoutKind.Surroundings, "main", "Aldric", true, null, null, Now - 1d, 6d, 80);

        Option().Condition(Args());

        Assert.AreEqual("Continue the ride", Variable("TAOM_FWATCH_OPTION"));
    }

    [TestMethod]
    public void Option_Blocked_IsGreyedWithTheReason()
    {
        _world.ReadCamp().Returns(Camp() with { AssignedScoutWounded = true });
        var args = Args();

        Assert.IsTrue(Option().Condition(args));

        Assert.IsFalse(args.IsEnabled);
        StringAssert.Contains(args.Tooltip.ToString(), "Mirien is too badly wounded to ride.");
    }

    [TestMethod]
    public void Option_EveryBlock_HasAReasonText()
    {
        foreach (ScoutBlock block in Enum.GetValues(typeof(ScoutBlock)))
        {
            if (block == ScoutBlock.None) continue;
            var offer = new ScoutOffer(ScoutOfferStatus.Blocked, block, "s", "Mirien", 10, false, 3);
            var reason = FarWatchTexts.BlockReason(offer);
            Assert.IsNotNull(reason, block.ToString());
            Assert.IsFalse(reason!.ToString().StartsWith("ERROR"), block + ": " + reason);
        }
    }

    [TestMethod]
    public void Option_TheCampFeatureOff_AndNoRide_IsNotShown()
    {
        _camps.Enabled.Returns(false);

        Assert.IsFalse(Option().Condition(Args()));
    }

    [TestMethod]
    public void Option_TheCampFeatureOff_ButARideRuns_StaysSoTheScoutCanBeCalledBack()
    {
        _camps.Enabled.Returns(false);
        Ride();

        Assert.IsTrue(Option().Condition(Args()));
    }

    [TestMethod]
    public void Option_AFaultInTheCondition_HidesTheOption_AndLogsOnce()
    {
        _world.ReadCamp().Returns(_ => throw new InvalidOperationException("no campaign"));
        var sut = Option();

        Assert.IsFalse(sut.Condition(Args()));
        Assert.IsFalse(sut.Condition(Args()));

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    // ---- The panel's greyed option goes stale: conditions re-run only when a menu is built or refreshed

    private FarWatchCampOption OpenPanelOption(out FarWatchCampOption sut)
    {
        sut = Option();
        _menus.CurrentMenuId.Returns(FieldCampCampaignBehavior.CampSubMenuId);
        return sut;
    }

    [TestMethod]
    public void Option_TheRestEndsWhileThePanelIsOpen_RefreshesItInPlace()
    {
        _store.SetRest(Now + 3d, "Mirien");
        OpenPanelOption(out var sut).Condition(Args());
        _world.NowHours.Returns(Now + 3d);

        sut.RefreshWhenStale();

        _menus.Received(1).RefreshCurrent();
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Option_ARideEndsWhileThePanelIsOpen_RefreshesIt()
    {
        Ride();
        OpenPanelOption(out var sut).Condition(Args());
        _store.ClearRide();
        _store.SetRest(Now + 8d, "Mirien");

        sut.RefreshWhenStale();

        _menus.Received(1).RefreshCurrent();
    }

    [TestMethod]
    public void Option_NothingChanged_RefreshesNothing()
    {
        _store.SetRest(Now + 3d, "Mirien");
        OpenPanelOption(out var sut).Condition(Args());

        sut.RefreshWhenStale();

        _menus.DidNotReceiveWithAnyArgs().RefreshCurrent();
    }

    [TestMethod]
    public void Option_TheHoursLeftOfARestTick_RefreshThePanelSoTheTooltipIsTrue()
    {
        _store.SetRest(Now + 3d, "Mirien");
        OpenPanelOption(out var sut).Condition(Args());
        _world.NowHours.Returns(Now + 1.5d);

        sut.RefreshWhenStale();

        _menus.Received(1).RefreshCurrent();
    }

    [TestMethod]
    public void Option_TheCampPanelIsNotOpen_RefreshesNothing_AndReadsNothing()
    {
        var sut = Option();
        _menus.CurrentMenuId.Returns("town");

        sut.RefreshWhenStale();

        _menus.DidNotReceiveWithAnyArgs().RefreshCurrent();
        _world.DidNotReceiveWithAnyArgs().ReadCamp();
    }

    [TestMethod]
    public void Option_AFaultInTheRefresh_LogsOnce()
    {
        var sut = Option();
        _menus.CurrentMenuId.Returns(FieldCampCampaignBehavior.CampSubMenuId);
        _world.ReadCamp().Returns(_ => throw new InvalidOperationException("no campaign"));

        sut.RefreshWhenStale();
        sut.RefreshWhenStale();

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void OptionConsequence_NoRide_OpensThePick()
    {
        Option().Consequence(Args());

        _menus.Received(1).EnsureMenuOpen(FarWatchPickMenu.PickMenuId);
    }

    [TestMethod]
    public void OptionConsequence_ARideRuns_ReopensTheWait()
    {
        Ride();

        Option().Consequence(Args());

        _menus.Received(1).EnsureMenuOpen(FarWatchWaitMenu.WaitMenuId);
    }

    [TestMethod]
    public void OptionConsequence_AMenuThatWillNotOpen_WarnsAndStaysPut()
    {
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(false);

        Option().Consequence(Args());

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    // ---- The pick

    private FarWatchPickMenu Pick() => new(_service, _world, _menus, _notifier, _logger);

    private void TwoSitesNear()
    {
        _world.When(w => w.CollectFiefs(Arg.Any<System.Collections.Generic.List<ScoutSite>>())).Do(call =>
        {
            var into = call.Arg<System.Collections.Generic.List<ScoutSite>>();
            into.Clear();
            into.Add(new ScoutSite { Id = "town_2", Name = "Bree", Distance = 20f, Dx = -5f, Dy = 1f });
            into.Add(new ScoutSite { Id = "castle_1", Name = "Dol Amroth", Distance = 10f, Dx = 0f, Dy = 10f });
        });
    }

    [TestMethod]
    public void Pick_OnInit_FillsTheBodyAndTheEightSlots_NearestFirst()
    {
        TwoSitesNear();
        var sut = Pick();

        sut.OnInit(Args());

        Assert.AreEqual("Where should Mirien ride?", Variable("TAOM_FWATCH_PICK"));
        Assert.AreEqual("Look over Dol Amroth (north)", Variable("TAOM_FWATCH_SITE_0"));
        Assert.AreEqual("Look over Bree (west)", Variable("TAOM_FWATCH_SITE_1"));
        Assert.AreEqual("", Variable("TAOM_FWATCH_SITE_2"));
        Assert.AreEqual("", Variable("TAOM_FWATCH_SITE_7"));
    }

    [TestMethod]
    public void Pick_Condition_ASlotBeyondTheSites_IsHidden_TheSurroundingsAlwaysShow()
    {
        TwoSitesNear();
        var sut = Pick();
        sut.OnInit(Args());

        Assert.IsTrue(sut.StartCondition(Args(), -1));
        Assert.IsTrue(sut.StartCondition(Args(), 0));
        Assert.IsTrue(sut.StartCondition(Args(), 1));
        Assert.IsFalse(sut.StartCondition(Args(), 2));
    }

    [TestMethod]
    public void Pick_Condition_WhenTheOfferIsNoLongerReady_GreysEveryChoiceWithTheReason()
    {
        var sut = Pick();
        sut.OnInit(Args());
        _world.ReadCamp().Returns(Camp() with { InEncounter = true });
        var args = Args();

        Assert.IsTrue(sut.StartCondition(args, -1));

        Assert.IsFalse(args.IsEnabled);
        StringAssert.Contains(args.Tooltip.ToString(), "encounter");
    }

    [TestMethod]
    public void Pick_StartSurroundings_StartsTheRide_AndOpensTheWait()
    {
        var sut = Pick();
        sut.OnInit(Args());

        sut.Start(ScoutKind.Surroundings, -1);

        Assert.AreEqual(ScoutKind.Surroundings, _store.Kind);
        _menus.Received(1).EnsureMenuOpen(FarWatchWaitMenu.WaitMenuId);
    }

    [TestMethod]
    public void Pick_StartASite_StoresTheSettlementIdAndName()
    {
        TwoSitesNear();
        var sut = Pick();
        sut.OnInit(Args());

        sut.Start(ScoutKind.Settlement, 1);

        Assert.AreEqual(ScoutKind.Settlement, _store.Kind);
        Assert.AreEqual("town_2", _store.TargetId);
        Assert.AreEqual("Bree", _store.TargetName);
    }

    [TestMethod]
    public void Pick_StartASlotBeyondTheSites_StartsNothing()
    {
        TwoSitesNear();
        var sut = Pick();
        sut.OnInit(Args());

        sut.Start(ScoutKind.Settlement, 5);

        Assert.IsFalse(_store.HasRide);
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Pick_StartWhileBlocked_StartsNothing_AndSaysWhy()
    {
        _store.SetRest(Now + 3d);
        var sut = Pick();

        sut.Start(ScoutKind.Surroundings, -1);

        Assert.IsFalse(_store.HasRide);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("resting")));
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Pick_TheWaitMenuWillNotOpen_DropsTheRideSilently()
    {
        _menus.EnsureMenuOpen(FarWatchWaitMenu.WaitMenuId).Returns(false);
        var sut = Pick();

        sut.Start(ScoutKind.Surroundings, -1);

        Assert.IsFalse(_store.HasRide, "nothing runs behind a menu the player cannot see");
        Assert.AreEqual(Now + 8d, _store.RestUntilHour, "a ride dropped silently still starts the rest");
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void Pick_AFaultAnywhere_LogsOnce()
    {
        _world.ReadCamp().Returns(_ => throw new InvalidOperationException("no campaign"));
        var sut = Pick();

        sut.OnInit(Args());
        sut.Start(ScoutKind.Surroundings, -1);
        Assert.IsFalse(sut.StartCondition(Args(), -1));

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Pick_Back_OpensTheCampPanel()
    {
        Pick().Back();

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    // ---- The wait

    private FarWatchWaitMenu Wait() => new(_service, _world, _landing, _presenter, _logger);

    private static MenuCallbackArgs WaitArgs(out GameMenu menu)
    {
        menu = (GameMenu)FormatterServices.GetUninitializedObject(typeof(GameMenu));
        var context = (MenuContext)FormatterServices.GetUninitializedObject(typeof(MenuContext));
        typeof(MenuContext).GetProperty("GameMenu")!.GetSetMethod(true)!.Invoke(context, new object[] { menu });
        return new MenuCallbackArgs(context, new TextObject("{=!}x"));
    }

    private static float ProgressOf(GameMenu menu) => menu.Progress;

    [TestMethod]
    public void Wait_OnInit_SetsTheTargetHours_TheProgress_AndTheTexts()
    {
        Ride(start: Now - 3d);
        var args = WaitArgs(out var menu);

        Wait().OnWaitInit(args);

        Assert.AreEqual(0.5f, ProgressOf(menu), 1e-5f);
        Assert.AreEqual(6f, menu.TargetWaitHours, 1e-5f);
        Assert.AreEqual("Mirien is sweeping the land around the camp.", Variable("TAOM_FWATCH_WAIT"));
        Assert.AreEqual("Call Mirien back", Variable("TAOM_FWATCH_RECALL"));
    }

    [TestMethod]
    public void Wait_OnInit_ASoloRide_OffersTheRideBackToCamp()
    {
        _store.StartRide(ScoutKind.Surroundings, "main", "Aldric", true, null, null, Now - 1d, 6d, 80);

        Wait().OnWaitInit(WaitArgs(out _));

        Assert.AreEqual("Ride back to camp", Variable("TAOM_FWATCH_RECALL"));
    }

    [TestMethod]
    public void Wait_OnInit_ASettlementRide_NamesTheTarget()
    {
        _store.StartRide(ScoutKind.Settlement, "scout_1", "Mirien", false, "town_9", "Edoras", Now - 1d, 4.2d, 150);
        var args = WaitArgs(out _);

        Wait().OnWaitInit(args);

        Assert.AreEqual("Mirien is riding to look over Edoras.", Variable("TAOM_FWATCH_WAIT"));
    }

    [TestMethod]
    public void Wait_OnInit_NoRide_Lands()
    {
        var args = WaitArgs(out _);

        Wait().OnWaitInit(args);

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_OnTick_WritesTheProgress()
    {
        Ride(start: Now - 1.5d);
        var args = WaitArgs(out var menu);

        Wait().OnWaitTick(args, default);

        Assert.AreEqual(0.25f, ProgressOf(menu), 1e-5f);
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Wait_OnTick_TheRideIsGone_Lands()
    {
        var args = WaitArgs(out _);

        Wait().OnWaitTick(args, default);

        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_OnTick_AFault_LogsOnce_DropsTheRide_AndLands()
    {
        Ride();
        var sut = Wait();

        // A tick handed no menu context throws inside the guard.
        sut.OnWaitTick(Args(), default);
        Ride();
        sut.OnWaitTick(Args(), default);

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
        _menus.Received().EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_OnInit_AFault_IsReported_DropsTheRideSilently_StartsTheRest_AndLands()
    {
        Ride();

        // An init handed no menu context throws inside the guard.
        Wait().OnWaitInit(Args());

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
        _menus.Received().EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_OnComplete_AFault_IsReported_AndTheMenuStillLands_WithTheRideNotPayingTwice()
    {
        Ride(start: Now - 6d);
        _world.IsHeroWithParty("scout_1").Returns(_ => throw new InvalidOperationException("no campaign"));

        Wait().OnWaitComplete(Args());

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide, "the ride was cleared before anything could throw");
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
        _menus.Received().EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_Recall_AFault_IsReported_AndTheMenuStillLands()
    {
        Ride(start: Now - 3d);
        _notifier.When(n => n.ShowMessage(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("no ui"));

        Wait().OnRecall();

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
        _menus.Received().EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_OnComplete_LandsThenShowsTheReport()
    {
        Ride(start: Now - 6d);

        Wait().OnWaitComplete(Args());

        Received.InOrder(() =>
        {
            _menus.EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
            _notifier.ShowReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        });
        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void Wait_OnComplete_FieldCampOff_LandsAndTellsTheCampLine_NotAReport()
    {
        Ride(start: Now - 6d);
        _camps.Enabled.Returns(false);

        Wait().OnWaitComplete(Args());

        _notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("camp was struck")));
        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void Wait_OnComplete_AlreadyCompletedByTheHourlyLook_JustLands()
    {
        Wait().OnWaitComplete(Args());

        _notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Wait_Recall_EndsTheRide_TellsHim_AndLands_WithNoReportAndNoXp()
    {
        Ride(start: Now - 3d);

        Wait().OnRecall();

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(0.25f, _store.BuffLevel, 1e-5f, "the sight he had reached stays");
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("nothing to report")));
        _notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
        _menus.Received(1).EnsureMenuOpen(FieldCampCampaignBehavior.CampSubMenuId);
    }
}
