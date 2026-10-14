using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarChronicle;

namespace TAOM.Tests.Features.WarChronicle;

/// <summary>
/// The War Chronicle's daily work runs at most once per elapsed campaign day. On a load the engine waits only
/// the elapsed part of the day before the next daily tick (v1.5.5 Campaign.cs:1024-1030), so a save made in
/// the first half of an elapsed day (which starts at CampaignStartTime, 09:00 on vanilla's calendar clock)
/// ticks that day a second time; the gate's saved last-run day stops the repeat.
/// </summary>
[TestClass]
public class WarChronicleDailyGateTests
{
    private WarChronicleDailyGate _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new WarChronicleDailyGate();

    [TestMethod]
    public void TryClaim_TwiceOnTheSameDay_ClaimsOnce()
    {
        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 12d));
        Assert.IsFalse(_sut.TryClaim(WarChronicleDailyGate.TickLane, 12d));
    }

    [TestMethod]
    public void TryClaim_TheNextDay_Claims()
    {
        _sut.TryClaim(WarChronicleDailyGate.TickLane, 12d);

        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 13d));
    }

    [TestMethod]
    public void TryClaim_AnEarlierDay_DoesNotClaim()
    {
        _sut.TryClaim(WarChronicleDailyGate.TickLane, 12d);

        Assert.IsFalse(_sut.TryClaim(WarChronicleDailyGate.TickLane, 11d));
    }

    [TestMethod]
    public void TryClaim_LaterTheSameDay_IsTheSameWholeDay()
    {
        // A tick after a load fires at a fractional time: day 12.2 and day 12.9 are both day 12.
        _sut.TryClaim(WarChronicleDailyGate.ChronicleLane, 12.2d);

        Assert.IsFalse(_sut.TryClaim(WarChronicleDailyGate.ChronicleLane, 12.9d));
    }

    [TestMethod]
    public void TryClaim_TheLanesAreIndependent()
    {
        _sut.TryClaim(WarChronicleDailyGate.ChronicleLane, 12d);

        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 12d), "the chronicle's claim must not stop the tick");
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-1d)]
    [DataRow(3e9)]
    public void TryClaim_ADayThatIsNotAFiniteWholeDayInRange_ClaimsNothing(double day)
    {
        Assert.IsFalse(_sut.TryClaim(WarChronicleDailyGate.TickLane, day));
        Assert.AreEqual(0, _sut.Snapshot().Count);
        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 0d), "a bad day leaves the lane unclaimed");
    }

    [TestMethod]
    public void SnapshotThenRestore_KeepsTheLastDayRun()
    {
        _sut.TryClaim(WarChronicleDailyGate.TickLane, 12d);
        _sut.TryClaim(WarChronicleDailyGate.ChronicleLane, 11d);
        var loaded = new WarChronicleDailyGate();

        loaded.Restore(_sut.Snapshot());

        Assert.IsFalse(loaded.TryClaim(WarChronicleDailyGate.TickLane, 12d));
        Assert.IsTrue(loaded.TryClaim(WarChronicleDailyGate.ChronicleLane, 12d));
    }

    [TestMethod]
    public void Restore_ReplacesWhatWasHeld()
    {
        _sut.TryClaim(WarChronicleDailyGate.TickLane, 40d);

        _sut.Restore(new Dictionary<string, int> { [WarChronicleDailyGate.ChronicleLane] = 5 });

        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 12d), "an older save's tick lane is unclaimed");
        Assert.IsFalse(_sut.TryClaim(WarChronicleDailyGate.ChronicleLane, 5d));
    }

    [TestMethod]
    public void ResetForNewSession_ForgetsEveryLane()
    {
        _sut.TryClaim(WarChronicleDailyGate.TickLane, 12d);

        _sut.ResetForNewSession();

        Assert.IsTrue(_sut.TryClaim(WarChronicleDailyGate.TickLane, 12d));
    }
}
