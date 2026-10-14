using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's per-campaign rows. The save format and its row rules are WarChronicleSaveCodecTests';
/// this is the in-memory store the codec fills and reads.
/// </summary>
[TestClass]
public class ChronicleStateStoreTests
{
    private readonly ChronicleStateStore _sut = new ChronicleStateStore();

    [TestMethod]
    public void SnapshotThenRestore_KeepsEveryRow()
    {
        _sut.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49, CloseDay = 69, Assaulted = true });
        _sut.Set(new ChronicleEventRecord("fellowship", ChronicleEventState.Occurred) { ResolvedDay = 20 });

        var restored = new ChronicleStateStore();
        restored.Restore(_sut.Snapshot());

        Assert.AreEqual(2, restored.Records.Count);
        Assert.IsTrue(restored.Get("hornburg")!.Assaulted);
        Assert.AreEqual(20d, restored.Get("fellowship")!.ResolvedDay);
    }

    [TestMethod]
    public void Restore_ReplacesWhatWasThere()
    {
        _sut.Set(new ChronicleEventRecord("stale", ChronicleEventState.Held) { ResolvedDay = 1 });

        _sut.Restore(new[] { new ChronicleEventRecord("fresh", ChronicleEventState.Pending) });

        Assert.IsNull(_sut.Get("stale"));
        Assert.IsNotNull(_sut.Get("fresh"));
    }

    [TestMethod]
    public void Set_ABlankId_IsIgnored()
    {
        _sut.Set(new ChronicleEventRecord(" ", ChronicleEventState.Pending));

        Assert.AreEqual(0, _sut.Records.Count);
    }

    [TestMethod]
    public void Set_TheSameId_ReplacesTheRow()
    {
        _sut.Set(new ChronicleEventRecord("a", ChronicleEventState.Pending));
        _sut.Set(new ChronicleEventRecord("a", ChronicleEventState.Held) { ResolvedDay = 3 });

        Assert.AreEqual(ChronicleEventState.Held, _sut.Snapshot().Single().State);
    }

    [TestMethod]
    public void ResetForNewSession_EmptiesTheStore()
    {
        _sut.Set(new ChronicleEventRecord("a", ChronicleEventState.Held));

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.Snapshot().Count);
    }

    [TestMethod]
    public void NotePhaseSeen_KeepsTheFirstDay()
    {
        _sut.NotePhaseSeen(WarPhase.IsengardWar, 101);
        _sut.NotePhaseSeen(WarPhase.IsengardWar, 150);

        Assert.AreEqual(101d, _sut.GetPhaseSeenDay(WarPhase.IsengardWar));
        Assert.IsNull(_sut.GetPhaseSeenDay(WarPhase.FullWar));
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.WarEnded)]
    public void NotePhaseSeen_APhaseNoEventAnchorsOn_IsIgnored(WarPhase phase)
    {
        _sut.NotePhaseSeen(phase, 10);

        Assert.AreEqual(0, _sut.PhaseSeenSnapshot().Count);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void NotePhaseSeen_ANonFiniteDay_IsIgnored(double day)
    {
        _sut.NotePhaseSeen(WarPhase.FullWar, day);

        Assert.IsNull(_sut.GetPhaseSeenDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void RestorePhaseSeenDays_ReplacesWhatWasThere()
    {
        _sut.NotePhaseSeen(WarPhase.IsengardWar, 5);

        _sut.RestorePhaseSeenDays(new Dictionary<WarPhase, double> { [WarPhase.FullWar] = 60 });

        Assert.IsNull(_sut.GetPhaseSeenDay(WarPhase.IsengardWar));
        Assert.AreEqual(60d, _sut.GetPhaseSeenDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void ResetForNewSession_ForgetsTheSeenDays()
    {
        _sut.NotePhaseSeen(WarPhase.IsengardWar, 101);

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.PhaseSeenSnapshot().Count);
    }
}
