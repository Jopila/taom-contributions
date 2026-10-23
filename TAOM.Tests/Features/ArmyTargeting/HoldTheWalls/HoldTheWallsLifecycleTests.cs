using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>#871: the per-campaign reset, the idle cost, and the daily debug line.</summary>
[TestClass]
public class HoldTheWallsLifecycleTests
{
    private HoldTheWallsRig _rig = null!;

    [TestInitialize]
    public void SetUp() => _rig = new HoldTheWallsRig();

    [TestMethod]
    public void ResetForNewSession_ForgetsEveryHoldAndCapture_WithNoEngineWrite()
    {
        _rig.Capture("town_A");
        _rig.Tick(1.0);
        _rig.Capture("town_B", HoldTheWallsRig.Army("lord_2"));
        _rig.Adapter.Writes.Clear();
        var reads = _rig.Adapter.Reads;

        _rig.Service.ResetForNewSession();
        _rig.Adapter.Armies["lord_1"].FortUnderSiege = true;
        _rig.Tick(1.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "campaign A's holds are gone and nothing is released: its parties do not exist");
        Assert.AreEqual(reads, _rig.Adapter.Reads, "nothing is left to read");
    }

    [TestMethod]
    public void ResetForNewSession_ZeroesTheDailyCounters()
    {
        _rig.Capture();
        _rig.Tick(1.0);
        _rig.Adapter.Armies["lord_1"].FortUnderSiege = true;
        _rig.Tick(1.0);

        _rig.Service.ResetForNewSession();
        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "holds=0, released=none, cohesionAtCapture=none, dispersedWhileHeld=0, inactivityDisbands=0");
    }

    [TestMethod]
    public void ResetForNewSession_ZeroesTheInactivityCount()
    {
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);

        _rig.Service.ResetForNewSession();
        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "inactivityDisbands=0");
    }

    [TestMethod]
    public void AnAiArmyDisbandedForInactivity_IsCounted_WhetherTheSwitchIsOnOrOff()
    {
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);
        _rig.Settings.HoldCapturedForts.Returns(false);
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);
        _rig.Settings.EnableArmyStrategicIntelligence.Returns(false);
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "inactivityDisbands=3");
    }

    [TestMethod]
    public void ThePlayersArmyDisbanded_IsNotCounted()
    {
        _rig.Service.OnInactivityDisband(isPlayersArmy: true);

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "inactivityDisbands=0");
    }

    [TestMethod]
    public void TheInactivityCount_StandsDownOutsideSinglePlayer()
    {
        _rig.CloseTheGate();
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);
        _rig.Coop.IsSessionActive.Returns(false);
        _rig.Dedicated.IsDedicatedServer.Returns(true);
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);
        _rig.Dedicated.IsDedicatedServer.Returns(false);
        _rig.Coop.ShouldDeferToHost.Returns(true);
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "inactivityDisbands=0");
    }

    [TestMethod]
    public void TheInactivityCount_ZeroesAfterTheDailyLine_AndTouchesNoEngineState()
    {
        _rig.Service.OnInactivityDisband(isPlayersArmy: false);
        _rig.Service.LogDailySummary();
        _rig.DebugLines.Clear();

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "inactivityDisbands=0");
        Assert.AreEqual(0, _rig.Adapter.Reads);
        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AnIdleHourlyPass_ReadsNothing_AndAsksNoSetting()
    {
        _rig.Tick(1.0);

        Assert.AreEqual(0, _rig.Adapter.Reads);
        _ = _rig.Settings.DidNotReceive().HoldCapturedForts;
        _ = _rig.Settings.DidNotReceive().EnableArmyStrategicIntelligence;
    }

    [TestMethod]
    public void TheDailyLine_NamesTheHoldsTheReleasesTheCohesionTheDispersedAndTheSlowestPass()
    {
        _rig.Adapter.Hours = 24.0 * 6 + 1.0;
        _rig.Capture("town_A");
        _rig.Capture("town_B", HoldTheWallsRig.Army("lord_2"));
        _rig.Capture("town_C", HoldTheWallsRig.Army("lord_3"));
        _rig.Adapter.Armies["lord_1"].Cohesion = 61f;
        _rig.Tick(1.0);
        _rig.Adapter.Armies["lord_2"].FortUnderSiege = true;
        _rig.Adapter.Armies.Remove("lord_3");
        _rig.Tick(1.0);

        _rig.Service.LogDailySummary();

        var line = _rig.DebugLines.Single();
        StringAssert.StartsWith(line, "[HoldTheWalls] day 6: holds=1, released=siege:1/");
        StringAssert.Contains(line, "cohesionAtCapture=61/80/80");
        StringAssert.Contains(line, "dispersedWhileHeld=1");
        StringAssert.Contains(line, "maxHourMs=");
    }

    [TestMethod]
    public void TheDailyLine_ListsEveryReleaseReason()
    {
        _rig.Capture();
        _rig.Tick(1.0);
        _rig.Settings.HoldCapturedForts.Returns(false);
        _rig.Tick(1.0);

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "released=siege:0/cohesion:0/food:0/off:1/coop:0/player:0/expired:0/moved:0");
    }

    [TestMethod]
    public void TheDailyLine_ZeroesTheCountersAfterWriting()
    {
        _rig.Capture();
        _rig.Tick(1.0);
        _rig.Service.LogDailySummary();
        _rig.DebugLines.Clear();

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "holds=1, released=none, cohesionAtCapture=none, dispersedWhileHeld=0, inactivityDisbands=0");
    }

    [TestMethod]
    public void TheDailyLine_WithAnUnreadableClock_NamesNoDay()
    {
        _rig.Adapter.Hours = double.NaN;

        _rig.Service.LogDailySummary();

        StringAssert.Contains(_rig.DebugLines.Single(), "day -1:");
    }
}
