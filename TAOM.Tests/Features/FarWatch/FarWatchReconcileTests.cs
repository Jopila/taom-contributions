using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// What ends or completes a ride from outside its wait menu (docs/features/far-watch.md "What ends the wait"): the main party's
/// encounters and map events and the hourly look. The listeners run for every encounter and map event in the world, so with no
/// ride they touch nothing. None of them ever switches a menu (the engine is opening its own, or the wait menu's next tick
/// lands the player: one exit path); a fault logs once and drops the ride.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchReconcileTests
{
    private const double Now = 1000d;

    private FarWatchStore _store = null!;
    private IFarWatchWorldAdapter _world = null!;
    private IGameMenuAdapter _menus = null!;
    private IFarWatchNotifier _notifier = null!;
    private IModLogger _logger = null!;
    private ICoopSessionProvider _coop = null!;
    private FarWatchService _service = null!;
    private FarWatchReconcile _sut = null!;

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
        _world.ReadCamp().Returns(new ScoutCampFacts { CampExists = true, CampReady = true });
        _world.IsHeroWithParty("scout_1").Returns(true);
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _notifier = Substitute.For<IFarWatchNotifier>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        var settings = Substitute.For<IHiddenHostsSettings>();
        var camps = Substitute.For<ICampSettingsProvider>();
        camps.Enabled.Returns(true);
        _service = new FarWatchService(_store, _world, new HiddenHostsService(settings, _store), _coop,
            Substitute.For<IDedicatedServerProvider>(), camps, _logger);
        _sut = new FarWatchReconcile(_service, _world, new FarWatchPresenter(_notifier), _logger);
    }

    private void Ride(double start = Now - 1d) =>
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, start, 6d, 150);

    // ---- The map-event listener

    [TestMethod]
    public void MapEvent_NoRide_TouchesNoAdapter_ForEveryBattleInTheWorld()
    {
        _sut.OnMapEventStarted(null!, null!, null!);

        _ = _world.DidNotReceive().IsMainPartyInMapEvent;
        _ = _world.DidNotReceive().NowHours;
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
    }

    [TestMethod]
    public void MapEvent_TheMainPartyIsInIt_EndsTheRide_WithTheBattleLine()
    {
        Ride();
        _world.IsMainPartyInMapEvent.Returns(true);

        _sut.OnMapEventStarted(null!, null!, null!);

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("Mirien") && m.Contains("trouble")));
    }

    [TestMethod]
    public void MapEvent_AnotherPartysBattle_LeavesTheRideAlone()
    {
        Ride();
        _world.IsMainPartyInMapEvent.Returns(false);

        _sut.OnMapEventStarted(null!, null!, null!);

        Assert.IsTrue(_store.HasRide);
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
    }

    [TestMethod]
    public void MapEvent_AFault_LogsOnce_AndDropsTheRide()
    {
        Ride();
        _world.IsMainPartyInMapEvent.Returns(_ => throw new InvalidOperationException("no campaign"));

        _sut.OnMapEventStarted(null!, null!, null!);
        _sut.OnMapEventStarted(null!, null!, null!);

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
    }

    // ---- The party-encounter listener (an attack on the camp opens a meeting menu with no battle behind it)

    [TestMethod]
    public void Encounter_NoRide_TouchesNoAdapter_ForEveryEncounterInTheWorld()
    {
        _sut.OnPartyEncounter(null!, null!);

        _world.DidNotReceiveWithAnyArgs().InvolvesMainParty(default!, default!);
        _ = _world.DidNotReceive().NowHours;
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
    }

    [TestMethod]
    public void Encounter_TheMainPartyIsInIt_EndsTheRide_WithTheBattleLine_AndSwitchesNoMenu()
    {
        Ride();
        _world.InvolvesMainParty(Arg.Any<PartyBase>(), Arg.Any<PartyBase>()).Returns(true);

        _sut.OnPartyEncounter(null!, null!);

        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("Mirien") && m.Contains("trouble")));
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _menus.DidNotReceiveWithAnyArgs().SwitchTo(default!);
        _menus.DidNotReceive().ExitToLast();
    }

    [TestMethod]
    public void Encounter_AnotherPartysMeeting_LeavesTheRideAlone()
    {
        Ride();
        _world.InvolvesMainParty(Arg.Any<PartyBase>(), Arg.Any<PartyBase>()).Returns(false);

        _sut.OnPartyEncounter(null!, null!);

        Assert.IsTrue(_store.HasRide);
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
    }

    [TestMethod]
    public void Encounter_AFault_LogsOnce_AndDropsTheRide()
    {
        Ride();
        _world.InvolvesMainParty(Arg.Any<PartyBase>(), Arg.Any<PartyBase>()).Returns(_ => throw new InvalidOperationException("no campaign"));

        _sut.OnPartyEncounter(null!, null!);
        Ride();
        _sut.OnPartyEncounter(null!, null!);

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
    }

    // ---- The hourly look

    [TestMethod]
    public void Hourly_NoRide_ReadsNothing()
    {
        _sut.OnHourlyTick();

        _world.DidNotReceiveWithAnyArgs().ReadCamp();
    }

    [TestMethod]
    public void Hourly_ARideInTime_ChangesNothing()
    {
        Ride();

        _sut.OnHourlyTick();

        Assert.IsTrue(_store.HasRide);
        _notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);
        _notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
    }

    [TestMethod]
    public void Hourly_TheCampWasStruck_EndsTheRide_WithTheCampLine_AndSwitchesNoMenu()
    {
        Ride();
        _world.ReadCamp().Returns(new ScoutCampFacts { CampExists = false });
        _menus.CurrentMenuId.Returns("town");

        _sut.OnHourlyTick();

        Assert.IsFalse(_store.HasRide);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("camp was struck")));
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _menus.DidNotReceive().ExitToLast();
    }

    private FarWatchWaitMenu WaitMenu() =>
        new(_service, _world, new FarWatchLanding(_menus, _world, _logger), new FarWatchPresenter(_notifier), _logger);

    private static MenuCallbackArgs WaitArgs()
    {
        var menu = (GameMenu)FormatterServices.GetUninitializedObject(typeof(GameMenu));
        var context = (MenuContext)FormatterServices.GetUninitializedObject(typeof(MenuContext));
        typeof(MenuContext).GetProperty("GameMenu")!.GetSetMethod(true)!.Invoke(context, new object[] { menu });
        return new MenuCallbackArgs(context, new TextObject("{=!}x"));
    }

    [TestMethod]
    public void Hourly_TheRideEndsWhileTheWaitMenuIsCurrent_SwitchesNoMenu_AndTheNextWaitTickLands()
    {
        Ride();
        _world.ReadCamp().Returns(new ScoutCampFacts { CampExists = true, CampReady = true, InEncounter = true });
        _menus.CurrentMenuId.Returns(FarWatchWaitMenu.WaitMenuId);

        _sut.OnHourlyTick();

        Assert.IsFalse(_store.HasRide);
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _menus.DidNotReceiveWithAnyArgs().SwitchTo(default!);
        _menus.DidNotReceive().ExitToLast();

        WaitMenu().OnWaitTick(WaitArgs(), default);

        _menus.Received(1).EnsureMenuOpen(TAOM.Features.FieldCamp.Hooks.FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Hourly_ADueRide_IsCompleted_AndItsReportOpensOverWhateverIsCurrent_WithNoLanding()
    {
        Ride(start: Now - 6d);
        _menus.CurrentMenuId.Returns("town_keep");

        _sut.OnHourlyTick();

        Assert.IsFalse(_store.HasRide);
        _notifier.Received(1).ShowReport(Arg.Is<string>(t => t.Contains("Mirien")), Arg.Any<string>(), Arg.Any<string>());
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        _world.Received(1).GrantScoutingXp("scout_1", Arg.Any<float>());
    }

    [TestMethod]
    public void Hourly_ADueRideWhileTheWaitMenuIsCurrent_ShowsTheReport_WithoutLanding_AndTheNextWaitTickLands()
    {
        Ride(start: Now - 6d);
        _menus.CurrentMenuId.Returns(FarWatchWaitMenu.WaitMenuId);

        _sut.OnHourlyTick();

        _notifier.Received(1).ShowReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);

        WaitMenu().OnWaitTick(WaitArgs(), default);

        _menus.Received(1).EnsureMenuOpen(TAOM.Features.FieldCamp.Hooks.FieldCampCampaignBehavior.CampSubMenuId);
    }

    [TestMethod]
    public void Hourly_ADueRideWhoseScoutIsGone_IsEnded_NotCompleted()
    {
        Ride(start: Now - 6d);
        _world.IsHeroWithParty("scout_1").Returns(false);

        _sut.OnHourlyTick();

        Assert.IsFalse(_store.HasRide);
        _notifier.Received(1).ShowMessage(Arg.Is<string>(m => m.Contains("did not come back")));
        _notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
    }

    [TestMethod]
    public void Hourly_AFault_LogsOnce_AndDropsTheRide()
    {
        Ride();
        _world.ReadCamp().Returns(_ => throw new InvalidOperationException("no campaign"));

        _sut.OnHourlyTick();
        Ride();
        _sut.OnHourlyTick();

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        Assert.IsFalse(_store.HasRide);
    }
}
