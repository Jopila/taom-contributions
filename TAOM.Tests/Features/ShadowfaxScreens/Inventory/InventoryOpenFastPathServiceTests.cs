using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

[TestClass]
public class InventoryOpenFastPathServiceTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private InventoryOpenFastPathService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.InventoryFastOpen.Returns(true);
        _sut = new InventoryOpenFastPathService(_settings, _clock, _logger);
    }

    [TestMethod]
    public void Enabled_FollowsTheToggle_PerCall()
    {
        Assert.IsTrue(_sut.Enabled);
        _settings.InventoryFastOpen.Returns(false);
        Assert.IsFalse(_sut.Enabled);
        _settings.InventoryFastOpen.Returns(true);
        Assert.IsTrue(_sut.Enabled);
    }

    [TestMethod]
    public void Enabled_WhenTheSettingThrows_IsOff()
    {
        _settings.InventoryFastOpen.Throws(new InvalidOperationException("mcm gone"));

        Assert.IsFalse(_sut.Enabled);
    }

    [TestMethod]
    public void ShouldSkipNameSort_FollowsTheToggle()
    {
        Assert.IsTrue(_sut.ShouldSkipNameSort());
        _settings.InventoryFastOpen.Returns(false);
        Assert.IsFalse(_sut.ShouldSkipNameSort());
    }

    [TestMethod]
    public void ReportSite_LogsOneLinePerSite_NoMatterHowOftenItIsReported()
    {
        _sut.ReportSite("name sort", 2, 2);
        _sut.ReportSite("name sort", 2, 2);
        _sut.ReportSite("concept lookup", 0, 2);
        _sut.ReportSite("concept lookup", 0, 2);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [ShadowfaxInventory] name sort: swapped 2 of 2 call sites",
            "WARN [ShadowfaxInventory] concept lookup: left vanilla (swapped 0 of 2 call sites)",
        }, _logger.Lines);
    }

    [TestMethod]
    public void TryGetConcept_WhenOff_NeverHits()
    {
        var source = new object();
        _sut.StoreConcept(0, source, "a");
        _settings.InventoryFastOpen.Returns(false);

        Assert.IsFalse(_sut.TryGetConcept(0, source, out _));
    }

    [TestMethod]
    public void TryGetConcept_OnePerSite_OncePerSource()
    {
        var source = new object();
        _sut.StoreConcept(0, source, "trade goods");
        _sut.StoreConcept(1, source, null);

        Assert.IsTrue(_sut.TryGetConcept(0, source, out var first));
        Assert.AreEqual("trade goods", first);
        Assert.IsTrue(_sut.TryGetConcept(1, source, out var second));
        Assert.IsNull(second);
        Assert.IsFalse(_sut.TryGetConcept(0, new object(), out _));
    }

    [TestMethod]
    public void TryGetConcept_UnknownSite_MissesWithoutThrowing()
    {
        Assert.IsFalse(_sut.TryGetConcept(7, new object(), out _));
        _sut.StoreConcept(7, new object(), "x");
    }

    [TestMethod]
    public void RowsPhase_OutsideAnOpen_IsNotCounted()
    {
        Assert.IsNull(_sut.BeginRows());
        _sut.EndRows(null);
        _sut.EndRows(5);

        _sut.BeginOpen();
        Assert.AreEqual("mode=trade, left=3, right=4, fast=on, rows=0.0 ms, sorts skipped=0, concepts cached=0/0",
            _sut.EndOpen("trade", 3, 4));
    }

    [TestMethod]
    public void Detail_ReportsModeCountsRowsAndCounters()
    {
        _sut.BeginOpen();
        var rows = _sut.BeginRows();
        _clock.Advance(41);
        _sut.EndRows(rows);
        _sut.ShouldSkipNameSort();
        _sut.ShouldSkipNameSort();
        var source = new object();
        _sut.TryGetConcept(0, source, out _);
        _sut.StoreConcept(0, source, "a");
        _sut.TryGetConcept(0, source, out _);
        _sut.TryGetConcept(0, source, out _);

        Assert.AreEqual("mode=inventory, left=312, right=88, fast=on, rows=41.0 ms, sorts skipped=2, concepts cached=2/3",
            _sut.EndOpen("inventory", 312, 88));
    }

    [TestMethod]
    public void Detail_WhenOff_SaysSoAndSkipsNothing()
    {
        _settings.InventoryFastOpen.Returns(false);
        _sut.BeginOpen();
        _sut.ShouldSkipNameSort();

        StringAssert.Contains(_sut.EndOpen("trade", 1, 2), "fast=off, rows=0.0 ms, sorts skipped=0");
    }

    [TestMethod]
    public void Detail_UnreadableCounts_PrintNa()
    {
        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(null, -1, -1), "mode=n/a, left=n/a, right=n/a");
    }

    [TestMethod]
    public void EndOpen_ClosesTheWindow_SoLaterRowsAreNotCounted()
    {
        _sut.BeginOpen();
        _sut.EndOpen("trade", 1, 1);

        Assert.IsNull(_sut.BeginRows());
        Assert.IsFalse(_sut.OpenActive);
    }

    [TestMethod]
    public void BeginOpen_ResetsTheCountersOfThePreviousOpen()
    {
        _sut.BeginOpen();
        _sut.ShouldSkipNameSort();
        _sut.EndOpen("trade", 1, 1);
        _sut.BeginOpen();

        StringAssert.Contains(_sut.EndOpen("trade", 1, 1), "sorts skipped=0");
    }

    [TestMethod]
    public void EverySurface_NeverThrows_WhenTheClockFails()
    {
        _clock.ThrowOnRead = 1;
        _sut.BeginOpen();

        Assert.IsNull(_sut.BeginRows());
        Assert.IsNotNull(_sut.EndOpen("trade", 1, 1));
        Assert.AreEqual(0, _logger.Lines.Count(l => l.StartsWith("ERROR")));
    }
}
