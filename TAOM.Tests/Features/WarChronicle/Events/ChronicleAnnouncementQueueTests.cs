using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's announcements: an in-memory queue, shown one at a time only when the map is free and
/// this peer is the authority (re-checked at show time), never stacked, not saved.
/// </summary>
[TestClass]
public class ChronicleAnnouncementQueueTests
{
    private IChroniclePresenter _presenter = null!;
    private ICoopSessionProvider _coop = null!;
    private IWarChronicleSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private readonly List<Action> _closers = new List<Action>();
    private readonly List<string> _shown = new List<string>();
    private ChronicleAnnouncementQueue _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _presenter = Substitute.For<IChroniclePresenter>();
        _presenter.IsMapFree().Returns(true);
        _presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>(), Arg.Any<Action>()))
            .Do(c => { _shown.Add(c.Arg<ChronicleAnnouncement>().EventId); _closers.Add(c.Arg<Action>()); });
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _settings = Substitute.For<IWarChronicleSettingsProvider>();
        _settings.ChronicleAnnouncements.Returns(true);
        _logger = Substitute.For<IModLogger>();
        _sut = new ChronicleAnnouncementQueue(_presenter, _coop, _settings, _logger);
    }

    private static ChronicleAnnouncement A(string id) => new ChronicleAnnouncement(id, "{=t}T", "{=b}B", null, 0);

    [TestMethod]
    public void TryShowNext_EmptyQueue_DoesNotTouchThePresenter()
    {
        _sut.TryShowNext();

        _presenter.DidNotReceive().IsMapFree();
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void TryShowNext_MapFree_ShowsTheOldestOnly()
    {
        _sut.Enqueue(A("one"));
        _sut.Enqueue(A("two"));

        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "one" }, _shown);
        Assert.AreEqual(1, _sut.Count);
    }

    [TestMethod]
    public void TryShowNext_WhileOneIsOpen_NeverStacksASecond()
    {
        _sut.Enqueue(A("one"));
        _sut.Enqueue(A("two"));
        _sut.TryShowNext();

        _sut.TryShowNext();
        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "one" }, _shown);
    }

    [TestMethod]
    public void TryShowNext_AfterTheOpenOneCloses_ShowsTheNext()
    {
        _sut.Enqueue(A("one"));
        _sut.Enqueue(A("two"));
        _sut.TryShowNext();

        _closers[0]();
        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "one", "two" }, _shown);
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void TryShowNext_MapBusy_WaitsWithTheItemQueued()
    {
        _presenter.IsMapFree().Returns(false);
        _sut.Enqueue(A("one"));

        _sut.TryShowNext();

        Assert.AreEqual(0, _shown.Count);
        Assert.AreEqual(1, _sut.Count);
    }

    [TestMethod]
    public void TryShowNext_NotTheAuthority_ShowsNothing()
    {
        _sut.Enqueue(A("one"));
        _coop.IsAuthority.Returns(false);

        _sut.TryShowNext();

        Assert.AreEqual(0, _shown.Count);
        _presenter.DidNotReceive().IsMapFree();
    }

    [TestMethod]
    public void Enqueue_AnnouncementsOff_QueuesNothing()
    {
        _settings.ChronicleAnnouncements.Returns(false);

        _sut.Enqueue(A("one"));

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void TryShowNext_PresenterThrows_DropsTheItemLogsAndDoesNotStick()
    {
        _presenter.When(p => p.Show(Arg.Is<ChronicleAnnouncement>(a => a.EventId == "bad"), Arg.Any<Action>()))
            .Do(_ => throw new InvalidOperationException("no screen"));
        _sut.Enqueue(A("bad"));
        _sut.Enqueue(A("good"));

        _sut.TryShowNext();
        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "bad", "good" }, _shown, "the failed show did not leave the queue stuck");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no screen")));
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheQueueAndTheOpenLatch()
    {
        _sut.Enqueue(A("one"));
        _sut.Enqueue(A("two"));
        _sut.TryShowNext();

        _sut.ResetForNewSession();
        _sut.Enqueue(A("three"));
        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "one", "three" }, _shown);
    }

    [TestMethod]
    public void AStaleCloserFromAnEarlierCampaign_DoesNotReleaseTheNewLatch()
    {
        _sut.Enqueue(A("one"));
        _sut.TryShowNext();
        var staleCloser = _closers[0];
        _sut.ResetForNewSession();
        _sut.Enqueue(A("two"));
        _sut.Enqueue(A("three"));
        _sut.TryShowNext();

        staleCloser();
        _sut.TryShowNext();

        CollectionAssert.AreEqual(new[] { "one", "two" }, _shown);
    }

    [TestMethod]
    public void Enqueue_PastTheCap_DropsTheOldest()
    {
        for (var i = 0; i < ChronicleAnnouncementQueue.MaxQueued + 2; i++)
            _sut.Enqueue(A("e" + i));

        Assert.AreEqual(ChronicleAnnouncementQueue.MaxQueued, _sut.Count);
        _sut.TryShowNext();
        Assert.AreEqual("e2", _shown[0]);
    }
}
