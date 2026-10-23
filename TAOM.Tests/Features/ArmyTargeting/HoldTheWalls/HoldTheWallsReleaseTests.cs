using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: a running hold. Each early-release reason restores what is left of the engine's own 10 hour pause; a hold whose
/// army is gone or left the realm's fortress is released the same way, and one that ran out of time is dropped with no
/// engine write, because the engine saves and expires the pause itself.
/// </summary>
[TestClass]
public class HoldTheWallsReleaseTests
{
    private HoldTheWallsRig _rig = null!;
    private FortArmyReading _army = null!;

    [TestInitialize]
    public void SetUp()
    {
        _rig = new HoldTheWallsRig();
        _army = _rig.Capture();
        _rig.Tick(1.0);
        _rig.Adapter.Writes.Clear();
    }

    [TestMethod]
    public void AHealthyHold_WritesNothingFurther()
    {
        _rig.Tick(5.0);
        _rig.Tick(5.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void TheFortComingUnderSiege_ReleasesTheArmy_RestoringTheRestOfVanillasPause()
    {
        _army.FortUnderSiege = true;

        _rig.Tick(4.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:5" }, _rig.Adapter.Writes, "capture at 1000, now 1005: 5 of the engine's 10 hours remain");
    }

    [TestMethod]
    public void ACohesionFall_ReleasesTheArmy()
    {
        _army.Cohesion = 39.9f;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void CohesionAtTheReleaseFloor_KeepsTheHold()
    {
        _army.Cohesion = 40f;

        _rig.Tick(2.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void LessThanADaysFood_ReleasesTheArmy()
    {
        _army.LowestFoodDays = 0.9f;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AnUnreadableCohesionOrFood_ReleasesTheArmy()
    {
        _army.Cohesion = float.NaN;
        _rig.Tick(1.0);
        Assert.AreEqual(1, _rig.Adapter.Writes.Count, "NaN fails the positive requirement");

        var second = new HoldTheWallsRig();
        var army = second.Capture();
        second.Tick(1.0);
        second.Adapter.Writes.Clear();
        army.LowestFoodDays = float.NaN;
        second.Tick(1.0);

        Assert.AreEqual(1, second.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AfterVanillasTenHours_AReleaseResumesTheAiOutright()
    {
        _army.FortUnderSiege = true;

        _rig.Tick(11.0);

        CollectionAssert.AreEqual(new[] { "resume:lord_1" }, _rig.Adapter.Writes, "capture at 1000, now 1012: the engine's own pause is over");
    }

    [TestMethod]
    public void ExactlyAtVanillasTenHours_AReleaseResumesTheAi()
    {
        _army.FortUnderSiege = true;

        _rig.Tick(9.0);

        CollectionAssert.AreEqual(new[] { "resume:lord_1" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AReleasedArmy_IsNotWrittenToAgain()
    {
        _army.FortUnderSiege = true;
        _rig.Tick(2.0);
        _rig.Adapter.Writes.Clear();

        _rig.Tick(1.0);
        _rig.Tick(1.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void ThePlayerJoiningTheArmy_ReleasesIt()
    {
        _army.IsPlayerLed = true;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void WhenTheTimeRunsOut_TheRecordIsDroppedWithNoWrite()
    {
        _rig.Tick(30.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "capture at 1000, hold to 1022, now 1031");
    }

    [TestMethod]
    public void AHoldPastItsEnd_IsNotReleasedEvenIfTheFortIsUnderSiege()
    {
        _army.FortUnderSiege = true;

        _rig.Tick(30.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "the engine's timer ended the pause already");
    }

    [TestMethod]
    public void AGoneArmy_IsReleased_SoTheLeaderKeepsNoLongerPauseThanVanillas()
    {
        _rig.Adapter.Armies.Remove("lord_1");

        _rig.Tick(2.0);
        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes, "the leader may still exist, with TAOM's longer pause on it");
        _rig.Adapter.Writes.Clear();

        _rig.Tick(1.0);
        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "released once");
    }

    [TestMethod]
    public void AnArmyThatLeftTheFort_IsReleased()
    {
        _army.InFort = false;
        _army.FortUnderSiege = true;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AFortThatChangedRealm_IsReleased()
    {
        _army.SameRealmAsFort = false;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AGoneOrMovedArmyPastVanillasTenHours_ResumesTheAi()
    {
        _army.SameRealmAsFort = false;

        _rig.Tick(11.0);

        CollectionAssert.AreEqual(new[] { "resume:lord_1" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ANewSiegeCaptureOfTheFort_ReleasesTheOldHoldAtTheNextPass_AndJudgesTheNewArmy()
    {
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);
        _army.SameRealmAsFort = false;
        var enemy = HoldTheWallsRig.Army("mordor_lord", "mordor");
        _rig.Adapter.Put("town_A", enemy);

        _rig.Tick(1.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:8" }, _rig.Adapter.Writes, "the old hold is released at the pass; the Evil army does not hold");
    }

    [TestMethod]
    public void ANewCaptureEventAlone_DropsNoHold_TheArmyStillQualifiesAndRestartsIt()
    {
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);

        _rig.Tick(1.0);

        Assert.AreEqual("hold:lord_1:21", _rig.Adapter.Writes.Single(), "only the pass judges a hold: this army is still inside and of the realm, so the new capture restarts its hold");
    }

    [TestMethod]
    public void AClaimantDecisionInTheSameRealm_DoesNotEndTheHold()
    {
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: false);
        _army.FortUnderSiege = true;

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes, "the hold still ran, so the siege released it");
    }

    [TestMethod]
    public void TurningTheSwitchOff_ReleasesEveryHold()
    {
        // lord_2 is captured at hour 1001, so the engine's own pause for it ends at 1011; now is 1003.
        var second = HoldTheWallsRig.Army("lord_2");
        _rig.Capture("town_B", second);
        _rig.Tick(0.0);
        _rig.Adapter.Writes.Clear();
        _rig.Settings.HoldCapturedForts.Returns(false);

        _rig.Tick(2.0);

        CollectionAssert.AreEquivalent(new[] { "hold:lord_1:7", "hold:lord_2:8" }, _rig.Adapter.Writes);
        _rig.Adapter.Writes.Clear();
        _rig.Tick(1.0);
        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "released once");
    }

    [TestMethod]
    public void TurningAiStrategicIntelligenceOff_ReleasesEveryHold()
    {
        _rig.Settings.EnableArmyStrategicIntelligence.Returns(false);

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes, "the hold switch is effective only while the group's master switch is on");
        _rig.Adapter.Writes.Clear();
        _rig.Tick(1.0);
        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void ARunningHold_WithAnUnreadableClock_WritesNothing_AndIsStillReleasedLater()
    {
        _army.FortUnderSiege = true;
        _rig.Adapter.Hours = double.NaN;

        _rig.Service.OnHourlyTick();
        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "no clock, no decision: the engine's timer still ends the pause");

        _rig.Adapter.Hours = 1003.0;
        _rig.Service.OnHourlyTick();
        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes, "the record was kept");
    }

    [TestMethod]
    public void ARunningHold_WithAnUnreadableClock_AndTheSwitchOff_ResumesTheAi()
    {
        _rig.Settings.HoldCapturedForts.Returns(false);
        _rig.Adapter.Hours = double.NaN;

        _rig.Service.OnHourlyTick();

        CollectionAssert.AreEqual(new[] { "resume:lord_1" }, _rig.Adapter.Writes, "vanilla's own remainder cannot be told, so the AI resumes");
    }

    [TestMethod]
    public void AThrowingWriteInEndAll_IsLogged_AndTheOtherHoldsAreStillReleased()
    {
        var second = HoldTheWallsRig.Army("lord_2");
        _rig.Capture("town_B", second);
        _rig.Tick(0.0);
        _rig.Adapter.Writes.Clear();
        _rig.Adapter.ThrowingWrites.Add("lord_1");
        _rig.Settings.HoldCapturedForts.Returns(false);

        _rig.Tick(2.0);

        Assert.AreEqual(1, _rig.Adapter.Writes.Count);
        StringAssert.StartsWith(_rig.Adapter.Writes.Single(), "hold:lord_2:");
        _rig.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[HoldTheWalls]") && s.Contains("lord_1") && s.Contains("write boom")));

        _rig.Adapter.ThrowingWrites.Clear();
        _rig.Adapter.Writes.Clear();
        _rig.Tick(1.0);
        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "both records are gone");
    }

    [TestMethod]
    public void TurningTheSwitchOffAndOn_DoesNotBringBackAReleasedHold()
    {
        _rig.Settings.HoldCapturedForts.Returns(false);
        _rig.Tick(1.0);
        _rig.Adapter.Writes.Clear();
        _rig.Settings.HoldCapturedForts.Returns(true);

        _rig.Tick(1.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void ACoopSessionGoingLive_ReleasesEveryHold()
    {
        _rig.CloseTheGate();

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ACoopModTaomCannotProbe_ReleasesEveryHold()
    {
        _rig.Coop.ShouldDeferToHost.Returns(true);

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ADedicatedServer_ReleasesEveryHold()
    {
        _rig.Dedicated.IsDedicatedServer.Returns(true);

        _rig.Tick(2.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:7" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AClosedGate_DropsACaptureNotYetJudged_AndRecordsNone()
    {
        var rig = new HoldTheWallsRig();
        rig.Capture();
        rig.CloseTheGate();
        rig.Tick(1.0);
        Assert.AreEqual(0, rig.Adapter.Writes.Count, "the pending capture is dropped, not held");

        rig.Coop.IsSessionActive.Returns(false);
        rig.Capture("town_B", HoldTheWallsRig.Army("other"));
        rig.CloseTheGate();
        rig.Tick(1.0);
        rig.Coop.IsSessionActive.Returns(false);
        rig.Tick(1.0);

        Assert.AreEqual(0, rig.Adapter.Writes.Count, "a gate that opens again does not revive what it dropped");
    }

    [TestMethod]
    public void ACaptureWhileTheGateIsClosed_IsNotRecorded()
    {
        var rig = new HoldTheWallsRig();
        rig.CloseTheGate();
        rig.Capture();
        rig.Coop.IsSessionActive.Returns(false);

        rig.Tick(1.0);

        Assert.AreEqual(0, rig.Adapter.Writes.Count);
        Assert.AreEqual(0, rig.Adapter.Reads);
    }

    [TestMethod]
    public void AThrowingRead_IsLogged_AndTheOtherHoldsAreStillStepped()
    {
        var second = HoldTheWallsRig.Army("lord_2");
        _rig.Capture("town_B", second);
        _rig.Tick(0.0);
        _rig.Adapter.Writes.Clear();
        _rig.Adapter.Throwing.Add("lord_1");
        second.FortUnderSiege = true;

        _rig.Tick(1.0);

        Assert.AreEqual(1, _rig.Adapter.Writes.Count, "lord_2 was released");
        StringAssert.StartsWith(_rig.Adapter.Writes.Single(), "hold:lord_2:");
        _rig.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[HoldTheWalls]") && s.Contains("lord_1") && s.Contains("boom")));

        _rig.Tick(1.0);
        _rig.Logger.Received(1).LogError(Arg.Any<string>());
    }
}
