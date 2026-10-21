using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 3: the exact stamps. A town, castle, village, encounter or conversation visit stops campaign time, and the
/// campaign tick event never fires then, so the game-menu event must stamp without a tick; the tick stamp (waiting, sieges)
/// is throttled in real time, not by the frame time the campaign hands the event. The handlers are called as the events
/// would; the effect is read back through the service.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsPlainSightBehaviorTests
{
    private static readonly long Interval = Stopwatch.Frequency / 2;

    private IHiddenHostsSettings _settings = null!;
    private HiddenHostsService _service = null!;
    private IPartyIntelAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private long _now;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _service = new HiddenHostsService(_settings, new NoScoutIntel());
        _adapter = Substitute.For<IPartyIntelAdapter>();
        _logger = Substitute.For<IModLogger>();
        _now = 1000;
    }

    private HiddenHostsPlainSightBehavior NewBehavior() => new(_service, _adapter, _logger, () => _now);

    private static PartyIntelSnapshot Occupant(string id, int healthy, bool visible = false) => new()
    {
        HasData = true, StringId = id, HealthyCount = healthy, OwnHealthyCount = healthy, IsVisible = visible, NowHour = 100,
        InPlainSight = true,
    };

    private void TheTownHolds(PartyIntelSnapshot lord, SettlementIntelSnapshot town)
    {
        _adapter.CollectPlainSight(Arg.Do<List<PartyIntelSnapshot>>(list => list.Add(lord))).Returns(town);
    }

    private static SettlementIntelSnapshot Town() => new()
    {
        HasData = true, StringId = "town_1", DefenderCount = 245, IsPlayerSettlement = true, NowHour = 100,
    };

    private string? Remembered(string id = "lord_1") => _service.Evaluate(new PartyIntelSnapshot
    {
        HasData = true, StringId = id, HealthyCount = 123, DistanceToMain = 70f, MainSeeingRange = 100f, SpottingRatio = 1f,
        NowHour = 101, IsVisible = true,
    }).CountText;

    [TestMethod]
    public void MenuOpened_StampsTheHostsAndTheSettlement_WithoutAnyTick()
    {
        TheTownHolds(Occupant("lord_1", 123), Town());
        var behavior = NewBehavior();

        behavior.OnMenuOpened(null!);

        Assert.AreEqual("~123", Remembered());
        Assert.AreEqual("~245", _service.EvaluateTooltipSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_1", DefenderCount = 245, DistanceToMain = 70f, MainSeeingRange = 100f, NowHour = 101,
        }).MemoryText);
    }

    [TestMethod]
    public void MenuOpened_IsNotThrottled_AndRestartsTheTickThrottle()
    {
        var behavior = NewBehavior();

        behavior.OnMenuOpened(null!);
        behavior.OnMenuOpened(null!);
        _adapter.Received(2).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());

        behavior.OnTick(0.25f);
        _adapter.Received(2).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());
    }

    [TestMethod]
    public void Tick_IsThrottledInRealTime_NotByTheFrameTimeItIsHanded()
    {
        var behavior = NewBehavior();

        behavior.OnTick(0f);
        _adapter.Received(1).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());

        _now += Interval - 1;
        behavior.OnTick(10f);
        _adapter.Received(1).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());

        _now += 1;
        behavior.OnTick(0f);
        _adapter.Received(2).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());
    }

    [TestMethod]
    public void Tick_StampsTheSameThingsAsTheMenu()
    {
        TheTownHolds(Occupant("lord_1", 123), Town());

        NewBehavior().OnTick(0.1f);

        Assert.AreEqual("~123", Remembered());
    }

    [TestMethod]
    public void TheSettingOff_StampsNothing_FromEitherTrigger()
    {
        _settings.Enabled.Returns(false);
        var behavior = NewBehavior();

        behavior.OnTick(1f);
        behavior.OnMenuOpened(null!);

        _adapter.DidNotReceive().CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());
    }

    [TestMethod]
    public void ASettingsReadThatThrows_DoesNotThrow_LogsOnce_AndStaysThrottled()
    {
        var behavior = NewBehavior();
        int reads = 0;
        _settings.Enabled.Returns(_ => { reads++; throw new InvalidOperationException("settings gone"); });

        behavior.OnTick(0.1f);
        behavior.OnTick(0.1f);

        Assert.AreEqual(1, reads);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ACollectThatThrows_IsReportedOnce_AndTheNextTriggerTriesAgain()
    {
        _adapter.CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>()).Throws(new InvalidOperationException("no campaign"));
        var behavior = NewBehavior();

        behavior.OnMenuOpened(null!);
        behavior.OnMenuOpened(null!);
        _now += Interval;
        behavior.OnTick(1f);

        _logger.Received(1).LogError(Arg.Any<string>());
        _adapter.Received(3).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());
    }

    [TestMethod]
    public void AFaultedTick_DoesNotRetryEveryFrame()
    {
        _adapter.CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>()).Throws(new InvalidOperationException("no campaign"));
        var behavior = NewBehavior();

        behavior.OnTick(1f);
        behavior.OnTick(1f);
        behavior.OnTick(1f);

        _adapter.Received(1).CollectPlainSight(Arg.Any<List<PartyIntelSnapshot>>());
    }

    [TestMethod]
    public void TheServicesOwnPredicate_DecidesWhoIsRemembered_NotTheBehavior()
    {
        TheTownHolds(Occupant("lord_1", 123) with { InPlayerClan = true }, Town() with { OwnedByPlayerClan = true });

        NewBehavior().OnMenuOpened(null!);

        Assert.AreEqual("HH1", _service.EncodeMemory(100), "own side: nothing is remembered, whatever the adapter handed over");
    }
}
