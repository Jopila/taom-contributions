using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
