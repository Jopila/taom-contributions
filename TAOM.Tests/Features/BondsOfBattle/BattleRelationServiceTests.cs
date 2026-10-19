using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.BondsOfBattle;

[TestClass]
public class BattleRelationServiceTests
{
    private static readonly BondsOfBattleSettings Defaults = new(true, 1, false, 5, true, 25f, true, 15);

    private IEnlistmentStateQuery _enlistment = null!;
    private IRandomSource _random = null!;
    private IRelationGrantService _grants = null!;
    private IBondsNoticeAdapter _notice = null!;
    private BattleRelationService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _enlistment.IsEnlisted.Returns(false);
        _random = Substitute.For<IRandomSource>();
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _grants = Substitute.For<IRelationGrantService>();
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(true);
        _notice = Substitute.For<IBondsNoticeAdapter>();
        _sut = new BattleRelationService(_enlistment, _random, _grants, _notice);
    }

    private static BattleParticipant Party(string? id, bool playerSide = true, bool clan = false,
        bool army = false, bool bandit = false, bool lord = true) =>
        new(id, id == null ? "" : "Name_" + id, playerSide, clan, army, bandit, lord);

    private static BattleSnapshot Battle(IEnumerable<BattleParticipant> parties, bool won = true, int enemyCasualties = 0,
        IEnumerable<SettlementNotables>? notables = null, bool hideout = false) =>
        new(parties.ToList(), won, enemyCasualties, (notables ?? Enumerable.Empty<SettlementNotables>()).ToList(), hideout);

    /// <summary>A bandit-only win with a notable town in reach: it reaches the gratitude path, so a gate that skips it is visible.</summary>
    private static BattleSnapshot BanditWin() =>
        Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80, notables: new[] { Town("t1", "n1") });

    private static SettlementNotables Town(string id, params string[] notableIds) =>
        new(id, "Town_" + id, notableIds.Select(n => new NotableInfo(n, "Notable_" + n)).ToList());

    // --- allies and enemies ---

    [TestMethod]
    public void ComputeGrants_AlliedLord_GetsTheAllyBonus()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("ally") }), Defaults);

        Assert.AreEqual(1, grants.Count);
        Assert.AreEqual("ally", grants[0].HeroId);
        Assert.AreEqual(1, grants[0].Delta);
        Assert.AreEqual(BattleGrantKind.FoughtTogether, grants[0].Kind);
        Assert.AreEqual("Name_ally", grants[0].HeroName);
    }

    [TestMethod]
    public void ComputeGrants_AllyBonusFollowsTheSetting()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("ally") }), Defaults with { AllyBonus = 4 });

        Assert.AreEqual(4, grants.Single().Delta);
    }

    [TestMethod]
    public void ComputeGrants_AllyBonusZero_GrantsNothing()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("ally") }), Defaults with { AllyBonus = 0 });

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_EnemyLord_PenaltyIsOffByDefault()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("foe", playerSide: false) }), Defaults);

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_EnemyLord_PenaltyOn_GrantsTheNegativeAmount()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[] { Party("foe", playerSide: false) }), settings);

        Assert.AreEqual(1, grants.Count);
        Assert.AreEqual(-5, grants[0].Delta);
        Assert.AreEqual(BattleGrantKind.FoughtAgainst, grants[0].Kind);
    }

    [TestMethod]
    public void ComputeGrants_EnemyPenaltyOnButZero_GrantsNothing()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true, EnemyPenalty = 0 };

        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party("foe", playerSide: false) }), settings).Count);
    }

    [TestMethod]
    public void ComputeGrants_AllyAndEnemyTogether_GetOneLineEach()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[] { Party("ally"), Party("foe", playerSide: false) }), settings);

        CollectionAssert.AreEquivalent(new[] { "ally", "foe" }, grants.Select(g => g.HeroId).ToList());
    }

    // --- switches ---

    [TestMethod]
    public void ComputeGrants_FeatureOff_GrantsNothing()
    {
        var withAlly = Battle(new[] { Party("ally"), Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") });

        var grants = _sut.ComputeGrants(withAlly, Defaults with { Enabled = false });

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_PlayerIsEnlisted_GrantsNothing()
    {
        _enlistment.IsEnlisted.Returns(true);

        var withAlly = Battle(new[] { Party("ally"), Party(null, playerSide: false, bandit: true) }, enemyCasualties: 100,
            notables: new[] { Town("t1", "n1") });

        var grants = _sut.ComputeGrants(withAlly, Defaults);

        Assert.AreEqual(0, grants.Count);
    }

    // --- exclusions ---

    [TestMethod]
    public void ComputeGrants_APlayerClanParty_IsExcluded()
    {
        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party("kin", clan: true) }), Defaults).Count);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ComputeGrants_ALeaderlessParty_IsExcluded(string? id)
    {
        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party(id) }), Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_APartyInThePlayersArmy_IsExcluded()
    {
        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party("armyLord", army: true) }), Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_ABanditLeader_IsNotALordToBond()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[] { Party("boss", playerSide: false, bandit: true) }), settings);

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_ANonLordLeader_IsExcluded()
    {
        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party("caravan", lord: false) }), Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_AnEnemyNonLordLeader_IsExcludedEvenWithThePenaltyOn()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[] { Party("merchant", playerSide: false, lord: false) }), settings);

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_AnExcludedPartyDoesNotBlockItsNeighbours()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("me", clan: true), Party("ally") }), Defaults);

        Assert.AreEqual("ally", grants.Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_OneLordWithTwoParties_GetsOneGrant()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("ally"), Party("ally") }), Defaults);

        Assert.AreEqual(1, grants.Count);
    }

    // --- bandit gratitude ---

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(9, 1)]
    [DataRow(10, 1)]
    [DataRow(25, 2)]
    [DataRow(45, 4)]
    [DataRow(100, 10)]
    [DataRow(101, 10)]
    [DataRow(5000, 10)]
    public void ComputeGrants_BanditWin_DeltaIsCasualtiesOverTenWithinOneAndTen(int casualties, int expected)
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: casualties,
            notables: new[] { Town("t1", "n1") });

        var grants = _sut.ComputeGrants(battle, Defaults);

        var grant = grants.Single();
        Assert.AreEqual(expected, grant.Delta);
        Assert.AreEqual("n1", grant.HeroId);
        Assert.AreEqual(BattleGrantKind.BanditGratitude, grant.Kind);
        Assert.AreEqual("Notable_n1", grant.HeroName);
        Assert.AreEqual("Town_t1", grant.SettlementName);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_NegativeCasualtiesStillGiveTheMinimum()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: -7,
            notables: new[] { Town("t1", "n1") });

        Assert.AreEqual(1, _sut.ComputeGrants(battle, Defaults).Single().Delta);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_PicksOneNotablePerSettlement()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "a", "b", "c"), Town("t2", "d", "e") });

        var grants = _sut.ComputeGrants(battle, Defaults);

        Assert.AreEqual(2, grants.Count);
        Assert.AreEqual("Town_t1", grants[0].SettlementName);
        Assert.AreEqual("Town_t2", grants[1].SettlementName);
        Assert.AreEqual("a", grants[0].HeroId);
        Assert.AreEqual("d", grants[1].HeroId);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_TheRandomSeamPicksTheNotable()
    {
        _random.Next(0, 3).Returns(2);
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "a", "b", "c") });

        Assert.AreEqual("c", _sut.ComputeGrants(battle, Defaults).Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_AnOutOfRangeRandomPickIsClampedIntoTheList()
    {
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(99);
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "a", "b") });

        Assert.AreEqual("b", _sut.ComputeGrants(battle, Defaults).Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_ASettlementWithoutNotablesIsSkipped()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("empty"), Town("t2", "d") });

        Assert.AreEqual("d", _sut.ComputeGrants(battle, Defaults).Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_BanditWin_ANotableListedTwiceIsGrantedOnce()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "a"), Town("t2", "a") });

        Assert.AreEqual(1, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_BanditLoss_GrantsNothing()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, won: false, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") });

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_WinAgainstLords_GrantsNoGratitude()
    {
        var battle = Battle(new[] { Party("foe", playerSide: false) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") });

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_WinAgainstBanditsAndALord_GrantsNoGratitude()
    {
        var battle = Battle(new[]
            {
                Party(null, playerSide: false, bandit: true),
                Party("foe", playerSide: false),
            }, enemyCasualties: 80, notables: new[] { Town("t1", "n1") });

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_NoEnemyPartiesRecorded_GrantsNoGratitude()
    {
        var battle = Battle(new[] { Party("ally") }, enemyCasualties: 80, notables: new[] { Town("t1", "n1") });

        Assert.IsFalse(_sut.ComputeGrants(battle, Defaults).Any(g => g.Kind == BattleGrantKind.BanditGratitude));
    }

    [TestMethod]
    public void ComputeGrants_BanditGratitudeOff_GrantsNothingForNotables()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") });

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults with { BanditGratitudeEnabled = false }).Count);
    }

    [TestMethod]
    public void ComputeGrants_BanditWinWithAnAlly_GrantsBothKinds()
    {
        var battle = Battle(new[] { Party("ally"), Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "n1") });

        var kinds = _sut.ComputeGrants(battle, Defaults).Select(g => g.Kind).ToList();

        CollectionAssert.AreEquivalent(new[] { BattleGrantKind.FoughtTogether, BattleGrantKind.BanditGratitude }, kinds);
    }

    [TestMethod]
    public void ComputeGrants_HideoutBattle_GrantsNoGratitude()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") }, hideout: true);

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_HideoutBattle_AnAllyLordStillGetsTheAllyBonus()
    {
        var battle = Battle(new[] { Party("ally"), Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") }, hideout: true);

        var grants = _sut.ComputeGrants(battle, Defaults);

        Assert.AreEqual("ally", grants.Single().HeroId);
        Assert.AreEqual(BattleGrantKind.FoughtTogether, grants.Single().Kind);
    }

    [TestMethod]
    public void ComputeGrants_AFieldBanditBattle_StillGetsGratitude()
    {
        Assert.AreEqual("n1", _sut.ComputeGrants(BanditWin(), Defaults).Single().HeroId);
    }

    // --- Apply ---

    [TestMethod]
    public void Apply_AGrantedLord_IsAppliedAndAnnounced()
    {
        _sut.Apply(Battle(new[] { Party("ally") }), Defaults);

        _grants.Received(1).Grant("ally", 1, Arg.Any<string>());
        _notice.Received(1).ShowFoughtTogether("Name_ally");
    }

    [TestMethod]
    public void Apply_AnEnemyLord_IsAnnouncedAsFoughtAgainst()
    {
        _sut.Apply(Battle(new[] { Party("foe", playerSide: false) }), Defaults with { EnemyPenaltyEnabled = true });

        _grants.Received(1).Grant("foe", -5, Arg.Any<string>());
        _notice.Received(1).ShowFoughtAgainst("Name_foe");
    }

    [TestMethod]
    public void Apply_ABanditNotable_IsAnnouncedWithTheirTown()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { Town("t1", "n1") });

        _sut.Apply(battle, Defaults);

        _grants.Received(1).Grant("n1", 3, Arg.Any<string>());
        _notice.Received(1).ShowBanditGratitude("Notable_n1", "Town_t1");
    }

    [TestMethod]
    public void Apply_WhenTheGrantIsRefused_NothingIsAnnounced()
    {
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(false);

        _sut.Apply(Battle(new[] { Party("ally") }), Defaults);

        _notice.DidNotReceiveWithAnyArgs().ShowFoughtTogether(default!);
    }

    [TestMethod]
    public void Apply_FeatureOff_TouchesNothing()
    {
        var withAlly = Battle(new[] { Party("ally"), Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80,
            notables: new[] { Town("t1", "n1") });

        _sut.Apply(withAlly, Defaults with { Enabled = false });

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _notice.DidNotReceiveWithAnyArgs().ShowBanditGratitude(default!, default!);
    }
}
