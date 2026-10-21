using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class OathLedgerTests
{
    private ICoopSessionProvider _coop = null!;
    private OathLedger _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _coop = Substitute.For<ICoopSessionProvider>();
        _sut = new OathLedger(_coop);
    }

    [TestMethod]
    public void Set_NormalizesToLowerCase()
    {
        Assert.IsTrue(_sut.Set("Oath.Of_Gondor"));

        Assert.IsTrue(_sut.Has("oath.of_gondor"));
        Assert.IsTrue(_sut.Has("OATH.OF_GONDOR"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("has space")]
    [DataRow("dash-ed")]
    public void Set_BadName_RefusedAndNotStored(string flag)
    {
        Assert.IsFalse(_sut.Set(flag));
        Assert.IsFalse(_sut.Has(flag));
    }

    [TestMethod]
    public void Set_OverlongName_Refused()
    {
        Assert.IsFalse(_sut.Set(new string('a', 65)));
    }

    [TestMethod]
    public void Writes_RefusedOnCoopClient()
    {
        _coop.IsCoopClient.Returns(true);

        Assert.IsFalse(_sut.Set("flag"));
        Assert.IsFalse(_sut.SetTopicLastDay("c", "t", 4));

        Assert.IsFalse(_sut.Has("flag"));
        Assert.IsFalse(_sut.TryGetTopicLastDay("c", "t", out _));
    }

    [TestMethod]
    public void Topics_TrackedPerCharacterAndTopic()
    {
        Assert.IsTrue(_sut.SetTopicLastDay("c1", "t1", 12));

        Assert.IsFalse(_sut.TryGetTopicLastDay("c2", "t1", out _));
        Assert.IsTrue(_sut.TryGetTopicLastDay("c1", "t1", out var day));
        Assert.AreEqual(12, day);
        Assert.IsFalse(_sut.TryGetTopicLastDay("c1", "t2", out _));
    }

    [TestMethod]
    public void ExportImport_RoundTrips()
    {
        _sut.Set("a");
        _sut.SetTopicLastDay("c", "t", 7);
        var data = _sut.Export();

        var other = new OathLedger(_coop);
        other.Import(data);

        Assert.IsTrue(other.Has("a"));
        Assert.IsTrue(other.TryGetTopicLastDay("c", "t", out var day));
        Assert.AreEqual(7, day);
    }

    [TestMethod]
    public void Import_NullMembersReadAsEmpty()
    {
        _sut.Import(new OathLedgerSaveData());

        Assert.IsFalse(_sut.Has("a"));
        Assert.AreEqual(0, _sut.Export().Flags.Count);
    }

    [TestMethod]
    public void Import_Null_LeavesEmpty()
    {
        _sut.Set("a");

        _sut.Import(null);

        Assert.IsFalse(_sut.Has("a"));
    }

    [TestMethod]
    public void Import_ClearsPreviousState()
    {
        _sut.Set("old");
        _sut.SetTopicLastDay("c", "old", 3);

        _sut.Import(new OathLedgerSaveData { Flags = new List<string> { "new" } });

        Assert.IsFalse(_sut.Has("old"));
        Assert.IsFalse(_sut.TryGetTopicLastDay("c", "old", out _));
        Assert.IsTrue(_sut.Has("new"));
    }

    [TestMethod]
    public void Import_SkipsBadFlagNames()
    {
        _sut.Import(new OathLedgerSaveData { Flags = new List<string> { "good", "bad name", null } });

        Assert.AreEqual(1, _sut.Export().Flags.Count);
    }
}
