using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
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
    private IAlignmentService _alignment = null!;
    private BattleRelationService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _enlistment.IsEnlisted.Returns(false);
        _random = Substitute.For<IRandomSource>();
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _grants = Substitute.For<IRelationGrantService>();
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        _notice = Substitute.For<IBondsNoticeAdapter>();
        _alignment = Substitute.For<IAlignmentService>();
        // free_f is Free, evil_f is Evil, anything else (or no id) is Neutral.
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(call =>
            call.ArgAt<string>(0) == "free_f" ? FactionSide.Free : call.ArgAt<string>(0) == "evil_f" ? FactionSide.Evil : FactionSide.Neutral);
        _sut = new BattleRelationService(_enlistment, _random, _grants, _notice, _alignment);
    }

    private static BattleParticipant Party(string? id, bool playerSide = true, bool clan = false,
        bool inArmy = false, bool bandit = false, bool lord = true, string? recipient = null, bool sideLeader = false) =>
        new(id, id == null ? "" : "Name_" + id, playerSide, clan, inArmy, bandit, lord, recipient, sideLeader);

    private static BattleSnapshot Battle(IEnumerable<BattleParticipant> parties, bool won = true, int enemyCasualties = 0,
        IEnumerable<SettlementNotables>? notables = null, bool hideout = false, string? playerFaction = "free_f",
        bool playerLeadsArmy = false, int thanks = 0, string? playerCulture = null) =>
        new(parties.ToList(), won, enemyCasualties, (notables ?? Enumerable.Empty<SettlementNotables>()).ToList(), hideout,
            playerFaction, playerCulture, playerLeadsArmy, thanks);

    /// <summary>A bandit-only win with a notable town in reach: it reaches the gratitude path, so a gate that skips it is visible.</summary>
    private static BattleSnapshot BanditWin() =>
        Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 80, notables: new[] { Town("t1", "n1") });

    private static SettlementNotables Town(string id, params string[] notableIds) => TownOf(id, "free_f", notableIds);

    private static SettlementNotables TownOf(string id, string? ownerFaction, params string[] notableIds) =>
        new(id, "Town_" + id, notableIds.Select(n => new NotableInfo(n, "Notable_" + n)).ToList(), ownerFaction, null);

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
    public void ComputeGrants_AMemberOfAnArmyThePlayerLeads_IsExcluded()
    {
        Assert.AreEqual(0, _sut.ComputeGrants(Battle(new[] { Party("armyLord", inArmy: true) }, playerLeadsArmy: true), Defaults).Count);
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

    // --- item 1: an army the player joined ---

    [TestMethod]
    public void ComputeGrants_AMemberOfAnArmyThePlayerJoined_Earns()
    {
        // The player is in this army but does not lead it (PlayerLeadsArmy false): its members are ordinary allies.
        var grants = _sut.ComputeGrants(
            Battle(new[] { Party("armyLeader", inArmy: true), Party("armyMember", inArmy: true) }, playerLeadsArmy: false), Defaults);

        CollectionAssert.AreEquivalent(new[] { "armyLeader", "armyMember" }, grants.Select(g => g.HeroId).ToList());
    }

    [TestMethod]
    public void ComputeGrants_ALordOutsideTheArmyThePlayerLeads_StillEarns()
    {
        var grants = _sut.ComputeGrants(
            Battle(new[] { Party("armyMember", inArmy: true), Party("outsider") }, playerLeadsArmy: true), Defaults);

        Assert.AreEqual("outsider", grants.Single().HeroId);
    }

    // --- item 2: counted once per effective recipient ---

    [TestMethod]
    public void ComputeGrants_TwoLordsOfOneClanWithTheSameRecipient_GiveOneGrant_ToTheFirstWhoFought()
    {
        var grants = _sut.ComputeGrants(
            Battle(new[] { Party("faramir", recipient: "denethor"), Party("boromir", recipient: "denethor") }), Defaults);

        Assert.AreEqual(1, grants.Count);
        Assert.AreEqual("faramir", grants[0].HeroId, "it still grants to and names the lord who fought");
        Assert.AreEqual("Name_faramir", grants[0].HeroName);
    }

    [TestMethod]
    public void ComputeGrants_TwoLordsWithTheirOwnRecipients_GiveTwoGrants()
    {
        var grants = _sut.ComputeGrants(
            Battle(new[] { Party("faramir", recipient: "faramir"), Party("boromir", recipient: "boromir") }), Defaults);

        CollectionAssert.AreEquivalent(new[] { "faramir", "boromir" }, grants.Select(g => g.HeroId).ToList());
    }

    [TestMethod]
    public void ComputeGrants_NoRecipient_FallsBackToTheLeadersOwnId()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("a"), Party("b"), Party("a") }), Defaults);

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, grants.Select(g => g.HeroId).ToList());
    }

    [TestMethod]
    public void ComputeGrants_TheEnemyPenalty_IsCountedOncePerRecipientToo()
    {
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[]
        {
            Party("foe1", playerSide: false, recipient: "chief"), Party("foe2", playerSide: false, recipient: "chief"),
        }), settings);

        Assert.AreEqual(1, grants.Count);
        Assert.AreEqual(-5, grants[0].Delta);
    }

    [TestMethod]
    public void ComputeGrants_AnAllyAndAnEnemyWithTheSameRecipient_AreCountedOnce()
    {
        // One recipient is paid once per battle, whichever side names him first.
        var settings = Defaults with { EnemyPenaltyEnabled = true };

        var grants = _sut.ComputeGrants(Battle(new[]
        {
            Party("ally", recipient: "chief"), Party("foe", playerSide: false, recipient: "chief"),
        }), settings);

        Assert.AreEqual(1, grants.Count, "one recipient is paid once per battle, whichever side first names him");
    }

    // --- item 4: a village in the other side's land does not thank ---

    [DataTestMethod]
    [DataRow("free_f", "evil_f", false)]
    [DataRow("evil_f", "free_f", false)]
    [DataRow("free_f", "free_f", true)]
    [DataRow("evil_f", "evil_f", true)]
    [DataRow("free_f", "neutral_f", true)]
    [DataRow("evil_f", "neutral_f", true)]
    [DataRow("neutral_f", "free_f", true)]
    [DataRow("neutral_f", "evil_f", true)]
    [DataRow("neutral_f", "neutral_f", true)]
    public void ComputeGrants_BanditGratitude_SkipsASettlementOnlyWhenOneSideIsFreeAndTheOtherEvil(
        string playerFaction, string ownerFaction, bool thanks)
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { TownOf("t1", ownerFaction, "n1") }, playerFaction: playerFaction);

        Assert.AreEqual(thanks ? 1 : 0, _sut.ComputeGrants(battle, Defaults).Count);
    }

    [TestMethod]
    public void ComputeGrants_BanditGratitude_OneVillageInEnemyLandIsSkipped_TheNextOneStillThanks()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { TownOf("enemyLand", "evil_f", "e1"), TownOf("ownLand", "free_f", "o1") });

        Assert.AreEqual("o1", _sut.ComputeGrants(battle, Defaults).Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_BanditGratitude_ASettlementWithNoOwnerFaction_IsNeutral_AndThanks()
    {
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { TownOf("t1", null, "n1") });

        Assert.AreEqual(1, _sut.ComputeGrants(battle, Defaults).Count);
    }

    // --- item 5: the side leader vanilla already thanks ---

    [TestMethod]
    public void ComputeGrants_TheSideLeaderVanillaThanks_IsSkippedOnAWin()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("leader", sideLeader: true), Party("other") }, thanks: 5), Defaults);

        Assert.AreEqual("other", grants.Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_TheSideLeaderVanillaThanks_IsGrantedOnALoss()
    {
        var grants = _sut.ComputeGrants(Battle(new[] { Party("leader", sideLeader: true) }, won: false, thanks: 5), Defaults);

        Assert.AreEqual("leader", grants.Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_ASkippedLeadersRecipient_IsNotPaidThroughAKinsman()
    {
        var grants = _sut.ComputeGrants(Battle(new[]
        {
            Party("kinsman", recipient: "chief"),
            Party("leader", sideLeader: true, recipient: "chief"),
            Party("stranger", recipient: "other"),
        }, thanks: 5), Defaults);

        Assert.AreEqual("stranger", grants.Single().HeroId, "the kinsman, listed first, is not paid either");
    }

    [TestMethod]
    public void ComputeGrants_TheSideLeader_WhenVanillaPaysNothing_EarnsOnAWin()
    {
        // A low-contribution win: vanilla's amount is 0, so there is no thank-you conversation and Bonds pays him.
        var grants = _sut.ComputeGrants(Battle(new[] { Party("leader", sideLeader: true) }, thanks: 0), Defaults);

        Assert.AreEqual("leader", grants.Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_TheSideLeaderOfAnArmyThePlayerJoined_EarnsOnAWin()
    {
        // Vanilla thanks no one who is in the player's army (PlayerEncounter.DoPlayerVictory), so Bonds pays him.
        var grants = _sut.ComputeGrants(
            Battle(new[] { Party("leader", sideLeader: true, inArmy: true) }, thanks: 5, playerLeadsArmy: false), Defaults);

        Assert.AreEqual("leader", grants.Single().HeroId);
    }

    [TestMethod]
    public void ComputeGrants_AThankedButNonBondableParty_StillReservesItsRecipient_SoItsKinsmanIsNotPaid()
    {
        // Vanilla's thank-you pays the side leader even when he is no lord Bonds would pay; his clan was thanked, so the
        // kinsman is not paid through the back door.
        var grants = _sut.ComputeGrants(Battle(new[]
        {
            Party("caravanLeader", sideLeader: true, lord: false, recipient: "chief"),
            Party("kinsman", recipient: "chief"),
        }, thanks: 5), Defaults);

        Assert.AreEqual(0, grants.Count);
    }

    [TestMethod]
    public void ComputeGrants_AnEnemySideLeader_DoesNotReserveItsRecipient()
    {
        var grants = _sut.ComputeGrants(Battle(new[]
        {
            Party("foeLeader", playerSide: false, sideLeader: true, recipient: "chief"),
            Party("ally", recipient: "chief"),
        }, thanks: 5), Defaults);

        Assert.AreEqual("ally", grants.Single().HeroId);
    }

    // The alignment fake is keyed on BOTH ids: a service that handed the owner's faction with the player's culture (or the
    // other way round) would read the wrong side.
    [TestMethod]
    public void ComputeGrants_BanditGratitude_ReadsEachSideWithItsOwnFactionAndCulture()
    {
        _alignment.ResolveSide("owner_f", "owner_c").Returns(FactionSide.Evil);
        _alignment.ResolveSide("player_f", "player_c").Returns(FactionSide.Free);
        var town = new SettlementNotables("t1", "Town_t1", new List<NotableInfo> { new("n1", "Notable_n1") }, "owner_f", "owner_c");
        var battle = Battle(new[] { Party(null, playerSide: false, bandit: true) }, enemyCasualties: 30,
            notables: new[] { town }, playerFaction: "player_f", playerCulture: "player_c");

        Assert.AreEqual(0, _sut.ComputeGrants(battle, Defaults).Count, "Free player, Evil owner: skipped");
    }

    // --- Apply ---

    [TestMethod]
    public void Apply_TheEngineLandsADifferentAmount_IsStillAnnounced()
    {
        _grants.Grant("ally", 1, Arg.Any<string>()).Returns(3);

        _sut.Apply(Battle(new[] { Party("ally") }), Defaults);

        _notice.Received(1).ShowFoughtTogether("Name_ally");
    }

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
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);

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
