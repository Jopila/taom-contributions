using System;
using System.Collections.Generic;
using System.Linq;
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
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
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

    private bool Hand(string? id = "lord_1", int influence = 50, bool playerClan = false, bool playerHero = false, string? owner = null) =>
        _sut.OnHandover(id, "Lord One", influence, playerClan, playerHero, owner);

    // --- the gain: a gift of influence/2, held to 10, a point either way, and nothing for a gift worth under 2 ---

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(-5)]
    [DataRow(int.MinValue)]
    public void ComputeGain_AGiftWorthLessThanTwo_IsZero(int influence)
    {
        Assert.AreEqual(0, _sut.ComputeGain(influence));
    }

    // No floor of 1: a gift worth 2 is a base of 1, so the roll -1 takes it to 0 and OnHandover's gate drops it.
    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    public void ComputeGain_AGiftWorthTwo_IsOnePlusTheRoll_AndMayBeZero(int roll, int expected)
    {
        _random.Next(-1, 2).Returns(roll);

        Assert.AreEqual(expected, _sut.ComputeGain(2));
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(11)]
    [DataRow(20)]
    [DataRow(50)]
    public void ComputeGain_OverTheThreeRolls_AveragesHalfTheInfluence(int influence)
    {
        var sum = 0;
        foreach (var roll in new[] { -1, 0, 1 })
        {
            _random.Next(-1, 2).Returns(roll);
            sum += _sut.ComputeGain(influence);
        }

        Assert.AreEqual(3 * Math.Min(10, influence / 2), sum);
    }

    [DataTestMethod]
    [DataRow(3, 1)]
    [DataRow(4, 2)]
    [DataRow(11, 5)]
    [DataRow(19, 9)]
    [DataRow(20, 10)]
    [DataRow(21, 10)]
    [DataRow(1000, 10)]
    [DataRow(int.MaxValue, 10)]
    public void ComputeGain_WithNoSpread_IsInfluenceOverTwoHeldToTen(int influence, int expected)
    {
        Assert.AreEqual(expected, _sut.ComputeGain(influence));
    }

    [TestMethod]
    public void ComputeGain_TheSpreadAddsOne_SoTheCapIsElevenAtMost()
    {
        _random.Next(-1, 2).Returns(1);

        Assert.AreEqual(6, _sut.ComputeGain(10));
        Assert.AreEqual(11, _sut.ComputeGain(500));
    }

    [TestMethod]
    public void ComputeGain_TheSpreadSubtractsOne_AndASmallGiftCanFallToZero()
    {
        _random.Next(-1, 2).Returns(-1);

        Assert.AreEqual(4, _sut.ComputeGain(10));
        Assert.AreEqual(0, _sut.ComputeGain(2));
        Assert.AreEqual(0, _sut.ComputeGain(3));
        Assert.AreEqual(1, _sut.ComputeGain(4));
        Assert.AreEqual(0, _sut.ComputeGain(1));
    }

    [DataTestMethod]
    [DataRow(5, 11)]
    [DataRow(int.MaxValue, 11)]
    [DataRow(-5, 9)]
    [DataRow(int.MinValue, 9)]
    public void ComputeGain_AnOutOfRangeRoll_IsClampedToPlusOrMinusOne(int roll, int expected)
    {
        // 20 influence is a base of 10, so the roll read as +1 gives 11 and read as -1 gives 9.
        _random.Next(-1, 2).Returns(roll);

        Assert.AreEqual(expected, _sut.ComputeGain(20));
    }

    // A random result needs the claim enumerated over every value of the random seam (testing-qa.md): for every way of
    // splitting a gift and every sequence of rolls, the total that lands in one day is at most 10. (One large Done can land
    // less than two smaller ones, so this is a ceiling, not "splitting never pays more".)
    [TestMethod]
    public void OnHandover_ForEverySplitAndEveryRollSequence_AtMostTenLandsInADay()
    {
        var checkedCases = 0;
        foreach (var total in new[] { 2, 3, 5, 12, 19, 20, 24, 40, 60, 1000 })
        {
            for (var parts = 1; parts <= 6; parts++)
            {
                var chunk = total / parts;
                if (chunk < 1)
                    continue;

                var sequences = (int)Math.Pow(3, parts);
                for (var sequence = 0; sequence < sequences; sequence++)
                {
                    var landed = 0;
                    var grants = Substitute.For<IRelationGrantService>();
                    grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call =>
                    {
                        landed += call.ArgAt<int>(1);
                        return call.ArgAt<int>(1);
                    });
                    var call = 0;
                    var random = Substitute.For<IRandomSource>();
                    var captured = sequence;
                    random.Next(-1, 2).Returns(_ => (captured / (int)Math.Pow(3, call++ % parts)) % 3 - 1);
                    var sut = new TroopHandoverService(grants, _settings, _enlistment, random, _notice, _coop);

                    for (var i = 0; i < parts - 1; i++)
                        sut.OnHandover("lord", "L", chunk, false, false, null);
                    sut.OnHandover("lord", "L", total - chunk * (parts - 1), false, false, null);

                    Assert.IsTrue(landed <= 10, $"total {total} in {parts} Dones, roll sequence {sequence}: {landed} landed");
                    checkedCases++;
                }
            }
        }

        Assert.IsTrue(checkedCases > 1000, "the enumeration ran");
    }

    // --- the daily cap: at most 10 relation per lord per in-game day, counted over what landed ---

    [TestMethod]
    public void OnHandover_SeveralDonesOnOneDay_PayAtMostTenInTotal()
    {
        var paid = 0;
        _grants.Grant("lord_1", Arg.Any<int>(), Arg.Any<string>()).Returns(call => { paid += call.ArgAt<int>(1); return call.ArgAt<int>(1); });

        for (var i = 0; i < 12; i++)
            _sut.OnHandover("lord_1", "Lord One", 10, false, false, null);

        Assert.AreEqual(10, paid, "each Done is worth 5; the third is cut to nothing at 10");
    }

    [TestMethod]
    public void OnHandover_TheDoneThatCrossesTheCap_IsCutToWhatIsLeft()
    {
        _sut.OnHandover("lord_1", "Lord One", 16, false, false, null);   // 8
        _grants.ClearReceivedCalls();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 16, false, false, null));

        _grants.Received(1).Grant("lord_1", 2, Arg.Any<string>());
    }

    [TestMethod]
    public void OnHandover_OnceTheDayIsFull_GrantsNothing_AndShowsNothing()
    {
        _sut.OnHandover("lord_1", "Lord One", 20, false, false, null);   // 10
        _grants.ClearReceivedCalls();
        _notice.ClearReceivedCalls();

        Assert.IsFalse(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
    }

    [TestMethod]
    public void OnHandover_TheNextDay_ResetsTheCap()
    {
        _sut.OnHandover("lord_1", "Lord One", 20, false, false, null);
        _sut.StartNewDay();
        _grants.ClearReceivedCalls();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));

        _grants.Received(1).Grant("lord_1", 10, Arg.Any<string>());
    }

    [TestMethod]
    public void OnHandover_TwoLords_AreCountedIndependently()
    {
        _sut.OnHandover("lord_1", "One", 20, false, false, null);

        Assert.IsTrue(_sut.OnHandover("lord_2", "Two", 20, false, false, null));

        _grants.Received(1).Grant("lord_2", 10, Arg.Any<string>());
    }

    [TestMethod]
    public void OnHandover_OnlyWhatLanded_CountsTowardsTheCap()
    {
        // The engine's diplomacy model scales every grant of 5 down to 1: ten of those landed fill the day, not two asked.
        _grants.Grant("lord_1", Arg.Any<int>(), Arg.Any<string>()).Returns(1);
        var shown = 0;
        _notice.When(n => n.ShowHandover(Arg.Any<string>())).Do(_ => shown++);

        for (var i = 0; i < 14; i++)
            _sut.OnHandover("lord_1", "Lord One", 10, false, false, null);

        Assert.AreEqual(10, shown, "ten landed points fill the day; the other four Dones show nothing");
    }

    [TestMethod]
    public void OnHandover_WhenCharmRaisesWhatLands_TenSmallDonesStopAtTenLanded()
    {
        // Grant lands Round(ask * 1.6): an ask of 1 lands 2, so five Dones fill the day and the sixth is refused.
        var landedTotal = 0;
        var calls = 0;
        _grants.Grant("lord_1", Arg.Any<int>(), Arg.Any<string>()).Returns(call =>
        {
            calls++;
            var landed = (int)Math.Round(call.ArgAt<int>(1) * 1.6, MidpointRounding.AwayFromZero);
            landedTotal += landed;
            return landed;
        });

        for (var i = 0; i < 10; i++)
            _sut.OnHandover("lord_1", "Lord One", 2, false, false, null);

        Assert.AreEqual(10, landedTotal);
        Assert.AreEqual(5, calls, "the day is full after five Dones; the other five ask for nothing");
    }

    [TestMethod]
    public void OnHandover_TwoKinsmenWithOneRelationOwner_ShareOneDay()
    {
        Assert.IsTrue(_sut.OnHandover("faramir", "Faramir", 20, false, false, "denethor"));

        Assert.IsFalse(_sut.OnHandover("boromir", "Boromir", 20, false, false, "denethor"));
        _grants.DidNotReceive().Grant("boromir", Arg.Any<int>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnHandover_KinsmenWithTheirOwnRelationOwners_AreCountedSeparately()
    {
        _sut.OnHandover("faramir", "Faramir", 20, false, false, "faramir");

        Assert.IsTrue(_sut.OnHandover("boromir", "Boromir", 20, false, false, "boromir"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void OnHandover_NoRelationOwner_KeysOnTheLord(string? owner)
    {
        _sut.OnHandover("lord_1", "Lord One", 20, false, false, owner);

        Assert.IsFalse(_sut.OnHandover("lord_1", "Lord One", 20, false, false, owner), "the same lord shares his day");
        Assert.IsTrue(_sut.OnHandover("lord_2", "Lord Two", 20, false, false, owner), "another lord is another key");
    }

    [TestMethod]
    public void OnHandover_TheGrantAndTheNotice_StillNameTheLordWhoWasHandedTheTroops()
    {
        _sut.OnHandover("faramir", "Faramir", 8, false, false, "denethor");

        _grants.Received(1).Grant("faramir", 4, Arg.Any<string>());
        _notice.Received(1).ShowHandover("Faramir");
    }

    [TestMethod]
    public void OnHandover_ARefusedGrant_DoesNotUseUpTheDay()
    {
        _grants.Grant("lord_1", Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        for (var i = 0; i < 5; i++)
            _sut.OnHandover("lord_1", "Lord One", 20, false, false, null);
        _grants.Grant("lord_1", Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        _grants.ClearReceivedCalls();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));

        _grants.Received(1).Grant("lord_1", 10, Arg.Any<string>());
    }

    // --- OnHandover ---

    [TestMethod]
    public void OnHandover_ADonation_GrantsTheGainAndAnnouncesIt()
    {
        var granted = Hand(influence: 8);

        Assert.IsTrue(granted);
        _grants.Received(1).Grant("lord_1", 4, Arg.Any<string>());
        _notice.Received(1).ShowHandover("Lord One");
    }

    [TestMethod]
    public void OnHandover_AGiftWorthOne_EarnsNothing_AndAnnouncesNothing()
    {
        Assert.IsFalse(Hand(influence: 1));

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
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
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        Assert.IsFalse(Hand());

        _notice.DidNotReceiveWithAnyArgs().ShowHandover(default!);
    }
}
