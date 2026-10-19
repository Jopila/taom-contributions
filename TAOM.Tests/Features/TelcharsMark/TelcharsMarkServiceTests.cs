using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.TelcharsMark;

namespace TAOM.Tests.Features.TelcharsMark;

[TestClass]
public class TelcharsMarkServiceTests
{
    private IUpgradeScreen _screen = null!;
    private TelcharsMarkService _sut = null!;

    private sealed class Row : IUpgradeRow
    {
        public ItemCandidate Candidate { get; set; } = null!;
        public UpgradeVerdict Verdict { get; set; }
    }

    private static Row MakeRow(int head, UpgradeVerdict verdict = UpgradeVerdict.None) =>
        new Row
        {
            Candidate = new ItemCandidate(
                new ItemUpgradeStats(UpgradeItemKind.Head, headArmor: head), true, true, true, true),
            Verdict = verdict,
        };

    [TestInitialize]
    public void Setup()
    {
        _screen = Substitute.For<IUpgradeScreen>();
        _screen.GetLoadout().Returns(new InventoryLoadout { Head = new ItemUpgradeStats(UpgradeItemKind.Head, headArmor: 10) });
        _sut = new TelcharsMarkService(new ItemUpgradeScorer());
    }

    private void SetRows(params Row[] rows) => _screen.GetRows().Returns(new List<IUpgradeRow>(rows));

    [TestMethod]
    public void Rescore_ScoresEveryRowAgainstOneLoadout()
    {
        var better = MakeRow(20);
        var worse = MakeRow(5, UpgradeVerdict.Better);
        SetRows(better, worse);

        _sut.Rescore(_screen, reapplyFilters: false);

        Assert.AreEqual(UpgradeVerdict.Better, better.Verdict);
        Assert.AreEqual(UpgradeVerdict.None, worse.Verdict, "a row that stops being an upgrade loses its verdict");
        _screen.Received(1).GetLoadout();
    }

    [TestMethod]
    public void Rescore_ReapplyRequested_ReappliesOnceAfterScoring()
    {
        SetRows(MakeRow(20), MakeRow(30));

        _sut.Rescore(_screen, reapplyFilters: true);

        Received.InOrder(() =>
        {
            _screen.GetRows();
            _screen.ReapplyFilters();
        });
        _screen.Received(1).ReapplyFilters();
    }

    [TestMethod]
    public void Rescore_ReapplyNotRequested_DoesNotReapply()
    {
        SetRows(MakeRow(20));
        _sut.Rescore(_screen, reapplyFilters: false);
        _screen.DidNotReceive().ReapplyFilters();
    }

    [TestMethod]
    public void ShouldHide_NoVerdictVanillaVisible_True() =>
        Assert.IsTrue(TelcharsMarkService.ShouldHide(MakeRow(1, UpgradeVerdict.None), vanillaFiltered: false));

    [TestMethod]
    public void ShouldHide_VanillaAlreadyFiltered_False() =>
        Assert.IsFalse(TelcharsMarkService.ShouldHide(MakeRow(1, UpgradeVerdict.None), vanillaFiltered: true));

    [TestMethod]
    public void ShouldHide_Better_False() =>
        Assert.IsFalse(TelcharsMarkService.ShouldHide(MakeRow(1, UpgradeVerdict.Better), vanillaFiltered: false));

    [TestMethod]
    public void ShouldHide_Sidegrade_False() =>
        Assert.IsFalse(TelcharsMarkService.ShouldHide(MakeRow(1, UpgradeVerdict.Sidegrade), vanillaFiltered: false));
}
