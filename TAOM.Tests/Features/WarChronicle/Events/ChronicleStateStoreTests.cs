using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The <c>chronicle</c> section of the War Chronicle payload: one row per event, state saved as its int.
/// A bad row is skipped alone with a warning, so the adoption rule re-creates it at session launch.
/// </summary>
[TestClass]
public class ChronicleStateStoreTests
{
    private readonly List<string> _warnings = new List<string>();
    private ChronicleStateStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogWarning(Arg.Any<string>())).Do(c => _warnings.Add(c.Arg<string>()));
        _sut = new ChronicleStateStore(logger);
    }

    [TestMethod]
    public void ToJsonThenRestore_KeepsEveryField()
    {
        _sut.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 49.5, CloseDay = 69.5, Assaulted = true });
        _sut.Set(new ChronicleEventRecord("fellowship", ChronicleEventState.Occurred) { ResolvedDay = 20.25 });

        var json = _sut.ToJson();
        var restored = new ChronicleStateStore(Substitute.For<IModLogger>());
        restored.RestoreFromJson(json);

        var hornburg = restored.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Active, hornburg.State);
        Assert.AreEqual(49.5, hornburg.OpenDay);
        Assert.AreEqual(69.5, hornburg.CloseDay);
        Assert.IsTrue(hornburg.Assaulted);
        Assert.IsNull(hornburg.ResolvedDay);
        Assert.AreEqual(20.25, restored.Get("fellowship")!.ResolvedDay);
        Assert.AreEqual(ChronicleEventState.Occurred, restored.Get("fellowship")!.State);
    }

    [TestMethod]
    public void ToJson_WritesTheStateAsItsInt()
    {
        _sut.Set(new ChronicleEventRecord("x", ChronicleEventState.Skipped) { ResolvedDay = 3 });

        StringAssert.Contains(_sut.ToJson(), "\"s\":6");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("[]")]
    public void RestoreFromJson_NothingSaved_LeavesTheStoreEmpty(string json)
    {
        _sut.Set(new ChronicleEventRecord("stale", ChronicleEventState.Active) { OpenDay = 1, CloseDay = 2 });

        _sut.RestoreFromJson(json);

        Assert.AreEqual(0, _sut.Records.Count);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void RestoreFromJson_NotAnArray_ResetsAndWarns()
    {
        _sut.RestoreFromJson("{\"id\":\"x\"}");

        Assert.AreEqual(0, _sut.Records.Count);
        Assert.AreEqual(1, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{\"s\":2}", "id")]
    [DataRow("{\"id\":\"\",\"s\":2}", "id")]
    [DataRow("{\"id\":\"a\",\"s\":99}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":-1}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":\"Held\"}", "state")]
    [DataRow("{\"id\":\"a\",\"s\":1,\"open\":1}", "window")]
    [DataRow("{\"id\":\"a\",\"s\":1,\"open\":\"NaN\",\"close\":2}", "window")]
    [DataRow("{\"id\":\"a\",\"s\":2,\"res\":\"x\"}", "res")]
    [DataRow("7", "object")]
    public void RestoreFromJson_ABadRow_IsSkippedAloneWithAWarning(string badRow, string fragment)
    {
        _sut.RestoreFromJson("[" + badRow + ",{\"id\":\"good\",\"s\":3,\"res\":40}]");

        Assert.IsNull(_sut.Get("a"));
        Assert.AreEqual(ChronicleEventState.Held, _sut.Get("good")!.State);
        Assert.IsTrue(_warnings.Any(w => w.Contains(fragment)), string.Join("\n", _warnings));
    }

    [TestMethod]
    public void RestoreFromJson_DuplicateId_KeepsTheFirst()
    {
        _sut.RestoreFromJson("[{\"id\":\"a\",\"s\":3},{\"id\":\"a\",\"s\":4}]");

        Assert.AreEqual(ChronicleEventState.Held, _sut.Get("a")!.State);
        Assert.AreEqual(1, _warnings.Count);
    }

    [TestMethod]
    public void RestoreFromJson_AnUnknownEventId_IsKeptSoANewerSaveSurvivesAnOlderBuild()
    {
        _sut.RestoreFromJson("[{\"id\":\"from_a_later_release\",\"s\":5,\"res\":12}]");

        Assert.AreEqual(ChronicleEventState.Lapsed, _sut.Get("from_a_later_release")!.State);
        StringAssert.Contains(_sut.ToJson(), "from_a_later_release");
    }

    [TestMethod]
    public void ResetForNewSession_EmptiesTheStore()
    {
        _sut.Set(new ChronicleEventRecord("a", ChronicleEventState.Held));

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.Records.Count);
        Assert.AreEqual("[]", _sut.ToJson());
    }
}
