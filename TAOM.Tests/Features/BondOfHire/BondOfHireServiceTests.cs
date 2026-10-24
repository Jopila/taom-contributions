using System.Linq;
using NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.BondOfHire;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// Bond of Hire (#867): a clan that signs on as a mercenary gets a minimum stay written to the engine's own "stay until" date,
/// but only when the date is nearer than the minimum. Every gate is tested to leave the clan unread, so a switch that is off or a
/// deferring peer never touches the engine.
/// </summary>
[TestClass]
public class BondOfHireServiceTests
{
    private IBondOfHireSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IMercenaryClanAdapter _clan = null!;
    private BondOfHireService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBondOfHireSettingsProvider>();
        _settings.MinimumDays.Returns(30);
        _coop = Substitute.For<ICoopSessionProvider>();
        _clan = Substitute.For<IMercenaryClanAdapter>();
        _clan.IsPlayerClan.Returns(false);
        _clan.RemainingStayDays.Returns(0f);
        _sut = new BondOfHireService(_settings, _coop);
    }

    [TestMethod]
    public void OnServiceStarted_NoDateSet_ExtendsToTheMinimum()
    {
        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsTrue(extended);
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_ADateShorterThanTheMinimum_ExtendsToTheMinimum()
    {
        _clan.RemainingStayDays.Returns(12.5f);

        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsTrue(extended);
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_ADatePastAlready_ExtendsToTheMinimum()
    {
        _clan.RemainingStayDays.Returns(-400f);

        Assert.IsTrue(_sut.OnServiceStarted(_clan));
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_ADateLongerThanTheMinimum_IsKept()
    {
        _clan.RemainingStayDays.Returns(50f);

        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsFalse(extended);
        _clan.DidNotReceive().SetStayUntilDaysFromNow(Arg.Any<int>());
    }

    [TestMethod]
    public void OnServiceStarted_ADateEqualToTheMinimum_IsKept()
    {
        _clan.RemainingStayDays.Returns(30f);

        Assert.IsFalse(_sut.OnServiceStarted(_clan));
        _clan.DidNotReceive().SetStayUntilDaysFromNow(Arg.Any<int>());
    }

    [TestMethod]
    public void OnServiceStarted_ADateJustUnderTheMinimum_IsExtended()
    {
        _clan.RemainingStayDays.Returns(29.99f);

        Assert.IsTrue(_sut.OnServiceStarted(_clan));
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_ARemainingTimeThatIsNaN_IsExtended_NotTakenForALongContract()
    {
        _clan.RemainingStayDays.Returns(float.NaN);

        Assert.IsTrue(_sut.OnServiceStarted(_clan));
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_TheMinimumIsZero_IsOff_AndNeverReadsTheClan()
    {
        _settings.MinimumDays.Returns(0);

        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsFalse(extended);
        _clan.DidNotReceive().SetStayUntilDaysFromNow(Arg.Any<int>());
        _ = _clan.DidNotReceive().RemainingStayDays;
        _ = _clan.DidNotReceive().IsPlayerClan;
    }

    [TestMethod]
    public void OnServiceStarted_ThePlayersClan_IsNeverBound()
    {
        _clan.IsPlayerClan.Returns(true);

        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsFalse(extended);
        _clan.DidNotReceive().SetStayUntilDaysFromNow(Arg.Any<int>());
        _ = _clan.DidNotReceive().RemainingStayDays;
    }

    [TestMethod]
    public void OnServiceStarted_ACoopPeerThatDefersToTheHost_LeavesTheClanAlone()
    {
        _coop.ShouldDeferToHost.Returns(true);

        var extended = _sut.OnServiceStarted(_clan);

        Assert.IsFalse(extended);
        _clan.DidNotReceive().SetStayUntilDaysFromNow(Arg.Any<int>());
        _ = _clan.DidNotReceive().IsPlayerClan;
        _ = _settings.DidNotReceive().MinimumDays;
    }

    [TestMethod]
    public void OnServiceStarted_ADedicatedServer_BindsTheClan()
    {
        // The dedicated server is the authority for AI clans, so the clan that signed on is bound there whenever the session
        // does not defer to a host. The service takes no dedicated-server input at all, so no server check can creep back in.
        _coop.ShouldDeferToHost.Returns(false);

        Assert.IsTrue(_sut.OnServiceStarted(_clan));
        _clan.Received(1).SetStayUntilDaysFromNow(30);
        var parameters = typeof(BondOfHireService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToArray();
        CollectionAssert.AreEqual(new[] { typeof(IBondOfHireSettingsProvider), typeof(ICoopSessionProvider) }, parameters);
    }

    [TestMethod]
    public void OnServiceStarted_ACoopHostThatDoesNotDefer_Extends()
    {
        _coop.IsSessionActive.Returns(true);
        _coop.ShouldDeferToHost.Returns(false);

        Assert.IsTrue(_sut.OnServiceStarted(_clan));
        _clan.Received(1).SetStayUntilDaysFromNow(30);
    }

    [TestMethod]
    public void OnServiceStarted_TheMinimumIsReadAtEachSigning_NotCached()
    {
        _settings.MinimumDays.Returns(45);
        _sut.OnServiceStarted(_clan);
        _settings.MinimumDays.Returns(120);
        _sut.OnServiceStarted(_clan);

        Received.InOrder(() =>
        {
            _clan.SetStayUntilDaysFromNow(45);
            _clan.SetStayUntilDaysFromNow(120);
        });
    }
}
