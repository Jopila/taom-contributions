using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

[TestClass]
public class ClanScreenFastPathServiceTests
{
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private ICoopPresenceProvider _coop = null!;
    private FakeStampClock _clock = null!;
    private ClanScreenFastPathService _sut = null!;
    private readonly object _a = new object();
    private readonly object _b = new object();

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.ClanFastOpen.Returns(true);
        _coop = Substitute.For<ICoopPresenceProvider>();
        _coop.IsCoopActive.Returns(false);
        _clock = new FakeStampClock { Frequency = 10000, Now = 5000 };
        _sut = new ClanScreenFastPathService(_settings, _coop, _clock);
    }

    [TestMethod]
    public void ShouldSkipListBuild_TheOwnerInsideItsRefresh_IsTrue()
    {
        var token = _sut.EnterRefresh(_a);

        Assert.IsNotNull(token);
        Assert.IsTrue(_sut.ShouldSkipListBuild(_a));
        Assert.IsTrue(_sut.ExitRefresh(token));
    }

    [TestMethod]
    public void EnterRefresh_ToggleOff_NoTokenAndNothingSkips()
    {
        _settings.ClanFastOpen.Returns(false);

        Assert.IsNull(_sut.EnterRefresh(_a));
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void EnterRefresh_CoopActive_NoTokenAndNothingSkips()
    {
        _coop.IsCoopActive.Returns(true);

        Assert.IsNull(_sut.EnterRefresh(_a));
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void EnterRefresh_SettingsProviderThrows_CountsAsOff()
    {
        _settings.ClanFastOpen.Throws(new InvalidOperationException("mcm gone"));

        Assert.IsNull(_sut.EnterRefresh(_a));
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void EnterRefresh_CoopProviderThrows_CountsAsOff()
    {
        _coop.IsCoopActive.Throws(new InvalidOperationException("coop probe gone"));

        Assert.IsNull(_sut.EnterRefresh(_a));
    }

    [TestMethod]
    public void ShouldSkipListBuild_AnotherInstance_IsFalseAndDoesNotMarkASkip()
    {
        var token = _sut.EnterRefresh(_a);

        Assert.IsFalse(_sut.ShouldSkipListBuild(_b));
        Assert.IsFalse(_sut.ExitRefresh(token), "no skip happened for the owner");
    }

    [TestMethod]
    public void ShouldSkipListBuild_NoRefreshInProgress_IsFalse()
    {
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void ShouldSkipListBuild_AfterTheRefreshExited_IsFalse()
    {
        _sut.ExitRefresh(_sut.EnterRefresh(_a));

        Assert.IsFalse(_sut.ShouldSkipListBuild(_a), "a direct RefreshPartiesList after the refresh must build");
    }

    [TestMethod]
    public void ExitRefresh_NoSkipHappened_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ExitRefresh(_sut.EnterRefresh(_a)));
    }

    [TestMethod]
    public void ExitRefresh_NestedEnter_RestoresThePreviousOwner()
    {
        var outer = _sut.EnterRefresh(_a);
        var inner = _sut.EnterRefresh(_b);
        Assert.IsTrue(_sut.ShouldSkipListBuild(_b));
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a), "the inner owner is current");

        Assert.IsTrue(_sut.ExitRefresh(inner));

        Assert.IsTrue(_sut.ShouldSkipListBuild(_a), "the outer owner is current again");
        Assert.IsFalse(_sut.ShouldSkipListBuild(_b));
        Assert.IsTrue(_sut.ExitRefresh(outer));
        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void ExitRefresh_NullToken_LeavesAnotherOwnerAlone()
    {
        var token = _sut.EnterRefresh(_a);

        Assert.IsFalse(_sut.ExitRefresh(null));

        Assert.IsTrue(_sut.ShouldSkipListBuild(_a));
        _sut.ExitRefresh(token);
    }

    [TestMethod]
    public void ExitRefresh_CalledTwiceForOneToken_StillLeavesNoOwner()
    {
        var token = _sut.EnterRefresh(_a);
        _sut.ExitRefresh(token);
        _sut.ExitRefresh(token);

        Assert.IsFalse(_sut.ShouldSkipListBuild(_a));
    }

    [TestMethod]
    public void Counting_OutsideAnOpenWindow_RecordsNothing()
    {
        var token = _sut.EnterRefresh(_a);
        _sut.ShouldSkipListBuild(_a);
        _sut.EndBuild(_sut.BeginBuild());
        _sut.ExitRefresh(token);

        _sut.BeginOpen();

        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void BeginBuild_OutsideAnOpenWindow_ReturnsZeroWithoutReadingTheClock()
    {
        _clock.ThrowOnRead = 1;

        Assert.AreEqual(0L, _sut.BeginBuild());
    }

    [TestMethod]
    public void EndOpen_AfterOneRunAndTwoSkippedBuilds_PinsTheDetailFormat()
    {
        _sut.BeginOpen();
        var token = _sut.EnterRefresh(_a);
        _sut.ShouldSkipListBuild(_a);
        _sut.ShouldSkipListBuild(_a);
        var start = _sut.BeginBuild();
        _clock.Advance(381);
        _sut.EndBuild(start);
        _sut.ExitRefresh(token);

        Assert.AreEqual("fast open on; list builds: 1 run, 2 skipped, 38.1 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_ToggleOff_SaysSoAndCountsEveryBuildAsRun()
    {
        _settings.ClanFastOpen.Returns(false);
        _sut.BeginOpen();
        for (var i = 0; i < 3; i++)
        {
            _sut.ShouldSkipListBuild(_a);
            var start = _sut.BeginBuild();
            _clock.Advance(100);
            _sut.EndBuild(start);
        }

        Assert.AreEqual("fast open off; list builds: 3 run, 0 skipped, 30.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_ClosesTheWindow_SoLaterBuildsAreNotCounted()
    {
        _sut.BeginOpen();
        _sut.EndOpen();
        Assert.AreEqual(0L, _sut.BeginBuild());

        _sut.BeginOpen();
        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndBuild_ZeroStart_CountsNothing()
    {
        _sut.BeginOpen();
        _sut.EndBuild(0);

        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_BeforeAnyBeginOpen_ReturnsAnEmptyDetail()
    {
        Assert.AreEqual("", _sut.EndOpen());
    }
}
