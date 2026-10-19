using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.BondsOfBattle;

[TestClass]
public class TroopHandoverServiceTests
{
    private static readonly BondsOfBattleSettings Defaults = new(true, 1, false, 5, true, 25f, true, 15);

    private IRelationGrantService _grants = null!;
    private IBondsOfBattleSettingsProvider _settings = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IRandomSource _random = null!;
    private IBondsNoticeAdapter _notice = null!;
    private ICoopSessionProvider _coop = null!;
    private TroopHandoverService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _grants = Substitute.For<IRelationGrantService>();
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(true);
        _settings = Substitute.For<IBondsOfBattleSettingsProvider>();
        _settings.Get().Returns(Defaults);
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _random = Substitute.For<IRandomSource>();
        _random.Next(-1, 2).Returns(0);
        _notice = Substitute.For<IBondsNoticeAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(false);
        _sut = new TroopHandoverService(_grants, _settings, _enlistment, _random, _notice, _coop);
    }

    private bool Hand(string? id = "lord_1", int influence = 50, bool playerClan = false, bool playerHero = false) =>
        _sut.OnHandover(id, "Lord One", influence, playerClan, playerHero);

    // --- the gain ---

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(int.MinValue)]
    public void ComputeGain_NoInfluence_IsZero(int influence)
    {
        Assert.AreEqual(0, _sut.ComputeGain(influence));
    }

    [DataTestMethod]
    [DataRow(1, 1)]
    [DataRow(9, 1)]
    [DataRow(10, 1)]
    [DataRow(25, 2)]
    [DataRow(55, 5)]
    [DataRow(100, 10)]
    [DataRow(101, 10)]
    [DataRow(100000, 10)]
    [DataRow(int.MaxValue, 10)]
    public void ComputeGain_WithNoSpread_IsInfluenceOverTenWithinOneAndTen(int influence, int expected)
    {
        Assert.AreEqual(expected, _sut.ComputeGain(influence));
    }

    [TestMethod]
    public void ComputeGain_TheSpreadAddsOne()
    {
        _random.Next(-1, 2).Returns(1);

        Assert.AreEqual(6, _sut.ComputeGain(55));
        Assert.AreEqual(11, _sut.ComputeGain(5000));
    }

    [TestMethod]
    public void ComputeGain_TheSpreadSubtractsOne_ButNeverBelowOne()
    {
        _random.Next(-1, 2).Returns(-1);

        Assert.AreEqual(4, _sut.ComputeGain(55));
        Assert.AreEqual(1, _sut.ComputeGain(5));
        Assert.AreEqual(1, _sut.ComputeGain(1));
    }

    [DataTestMethod]
    [DataRow(5, 11)]
    [DataRow(int.MaxValue, 11)]
    [DataRow(-5, 9)]
    [DataRow(int.MinValue, 9)]
    public void ComputeGain_AnOutOfRangeRoll_IsClampedToPlusOrMinusOne(int roll, int expected)
    {
        // 100 influence is a base of 10, so the roll read as +1 gives 11 and read as -1 gives 9.
        _random.Next(-1, 2).Returns(roll);

        Assert.AreEqual(expected, _sut.ComputeGain(100));
        Assert.AreEqual(roll > 0 ? 2 : 1, _sut.ComputeGain(10), "base 1 plus the clamped roll, never below one");
    }

    // --- OnHandover ---

    [TestMethod]
    public void OnHandover_ADonation_GrantsTheGainAndAnnouncesIt()
    {
        var granted = Hand(influence: 55);

        Assert.IsTrue(granted);
        _grants.Received(1).Grant("lord_1", 5, Arg.Any<string>());
        _notice.Received(1).ShowHandover("Lord One");
    }

    [TestMethod]
    public void OnHandover_ZeroInfluence_DoesNothing()
    {
        Assert.IsFalse(Hand(influence: 0));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void OnHandover_NoLord_DoesNothing(string? heroId)
    {
        Assert.IsFalse(Hand(heroId));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_FeatureOff_DoesNothing()
    {
        _settings.Get().Returns(Defaults with { Enabled = false });

        Assert.IsFalse(Hand());

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_HandoverSwitchOff_DoesNothing()
    {
        _settings.Get().Returns(Defaults with { TroopHandoverEnabled = false });

        Assert.IsFalse(Hand());

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_Enlisted_DoesNothing()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.IsFalse(Hand());

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_CoopClientDefersToTheHost_DoesNothing()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(Hand());

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
    }

    [TestMethod]
    public void OnHandover_PlayerClanLord_DoesNothing()
    {
        Assert.IsFalse(Hand(playerClan: true));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_PlayerHero_DoesNothing()
    {
        Assert.IsFalse(Hand(playerHero: true));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnHandover_WhenTheGrantIsRefused_NothingIsAnnounced()
    {
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(false);

        Assert.IsFalse(Hand());

        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
    }
}
