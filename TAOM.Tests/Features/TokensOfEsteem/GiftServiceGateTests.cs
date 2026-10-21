using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// When the gift line shows, when it is greyed, and when the hero refuses. Every rule is decided by the service from raw
/// facts, so each one fails on its own fact here. The alignment service is the real one, built from the shipped data.
/// </summary>
[TestClass]
public class GiftServiceGateTests
{
    private GiftFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GiftFixture();

    // The cooldown is the ledger's, keyed on the relation owner, which is the hero himself unless a test says otherwise.
    private GiftGate LordGate(int? lastDay = null)
    {
        Stamp(lastDay);
        return _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger);
    }

    private GiftGate CompanionGate(int? lastDay = null)
    {
        _f.Recipient = GiftFixture.Companion();
        Stamp(lastDay);
        return _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger);
    }

    private void Stamp(int? day)
    {
        if (day is { } d)
            _f.Ledger.Stamp(GiftFixture.HeroId, d);
    }

    [TestMethod]
    public void Gate_LordInPeace_IsOpen()
    {
        Assert.AreEqual(GiftGateKind.Open, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_CompanionInTheParty_IsOpen()
    {
        Assert.AreEqual(GiftGateKind.Open, CompanionGate().Kind);
    }

    [TestMethod]
    public void Gate_FeatureOff_IsHiddenOnBothEntries()
    {
        _f.Settings = _f.Settings with { Enabled = false };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
        Assert.AreEqual(GiftGateKind.Hidden, CompanionGate().Kind);
    }

    [TestMethod]
    public void Gate_NoPlayerFacts_IsHidden()
    {
        _f.Player = null;

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_UnknownRecipient_IsHidden()
    {
        _f.Recipient = null;

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_LordWhoIsAChild_IsHidden()
    {
        _f.Recipient = GiftFixture.Lord() with { IsChild = true };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_LordEntryForSomeoneWhoIsNoLord_IsHidden()
    {
        _f.Recipient = GiftFixture.Lord() with { IsLord = false };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_LordEntryForACompanion_IsHidden()
    {
        _f.Recipient = GiftFixture.Companion();

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_CompanionEntryForALord_IsHidden()
    {
        Assert.AreEqual(GiftGateKind.Hidden, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_CompanionNotInThePlayersParty_IsHidden()
    {
        _f.Recipient = GiftFixture.Companion() with { IsInPlayerParty = false };

        Assert.AreEqual(GiftGateKind.Hidden, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_ClanMemberInThePartyWhoIsNoCompanion_IsHiddenOnTheCompanionEntry()
    {
        _f.Recipient = GiftFixture.Companion() with { IsPlayerCompanion = false };

        Assert.AreEqual(GiftGateKind.Hidden, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_LordAtWarWithThePlayer_IsHidden()
    {
        _f.Recipient = GiftFixture.Lord() with { IsAtWarWithPlayer = true };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_WarBeatsCooldownHostilityAndAlignment()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };
        _f.Recipient = GiftFixture.Lord(relation: -90) with { IsAtWarWithPlayer = true };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate(lastDay: 100).Kind);
    }

    [DataTestMethod]
    [DataRow(100, 7)]
    [DataRow(99, 6)]
    [DataRow(98, 5)]
    [DataRow(94, 1)]
    public void Gate_InsideTheCooldown_ReportsTheDaysLeft(int lastDay, int expectedLeft)
    {
        // Today is day 100 and the cooldown is 7 days.
        var gate = LordGate(lastDay);

        Assert.AreEqual(GiftGateKind.Cooldown, gate.Kind);
        Assert.AreEqual(expectedLeft, gate.DaysLeft);
    }

    [DataTestMethod]
    [DataRow(93)]
    [DataRow(50)]
    [DataRow(0)]
    public void Gate_CooldownOver_IsOpen(int lastDay)
    {
        Assert.AreEqual(GiftGateKind.Open, LordGate(lastDay).Kind);
    }

    [TestMethod]
    public void Gate_NeverGifted_IsOpen()
    {
        Assert.AreEqual(GiftGateKind.Open, LordGate(null).Kind);
    }

    [TestMethod]
    public void Gate_CooldownStampedInTheFuture_IsOpen()
    {
        // A damaged or hand-edited save must not lock a hero out for years.
        Assert.AreEqual(GiftGateKind.Open, LordGate(lastDay: 5000).Kind);
    }

    [TestMethod]
    public void Gate_CooldownFollowsTheSetting()
    {
        _f.Settings = _f.Settings with { CooldownDays = 30 };

        var gate = LordGate(lastDay: 80);

        Assert.AreEqual(GiftGateKind.Cooldown, gate.Kind);
        Assert.AreEqual(10, gate.DaysLeft);
    }

    [TestMethod]
    public void Gate_CooldownAppliesToCompanionsToo()
    {
        Assert.AreEqual(GiftGateKind.Cooldown, CompanionGate(lastDay: 99).Kind);
    }

    [DataTestMethod]
    [DataRow(-50, GiftGateKind.Open)]
    [DataRow(-49, GiftGateKind.Open)]
    [DataRow(0, GiftGateKind.Open)]
    [DataRow(-51, GiftGateKind.RefusesHostile)]
    [DataRow(-100, GiftGateKind.RefusesHostile)]
    public void Gate_LordBelowMinus50_RefusesWithCause(int relation, GiftGateKind expected)
    {
        _f.Recipient = GiftFixture.Lord(relation);

        Assert.AreEqual(expected, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_CompanionBelowMinus50_IsStillOpen()
    {
        _f.Recipient = GiftFixture.Companion(relation: -80);

        Assert.AreEqual(GiftGateKind.Open, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_HostileLordInCooldown_ShowsTheCooldown()
    {
        _f.Recipient = GiftFixture.Lord(relation: -80);

        Assert.AreEqual(GiftGateKind.Cooldown, LordGate(lastDay: 99).Kind);
    }

    // Faction and culture ids are the shipped ones (execution/alignment.json). empire_w, gondor, rivendell, vlandia, erebor are
    // Free; empire_s (Mordor's realm; mordor is its culture), mordor, empire (Dunland), isengard, aserai are Evil; battania
    // (Khand), umbar, shaghana are Neutral. clan_test_1 and vakken are not listed, so they read Neutral. A hero without a
    // kingdom is its clan (player_faction for the player) with the clan's culture.
    [DataTestMethod]
    [DataRow("empire_w", "gondor", "empire_s", "mordor", GiftGateKind.RefusesAlignment)]
    [DataRow("vlandia", "vlandia", "empire", "empire", GiftGateKind.RefusesAlignment)]
    [DataRow("erebor", "erebor", "isengard", "isengard", GiftGateKind.RefusesAlignment)]
    [DataRow("empire_w", "gondor", "empire_w", "gondor", GiftGateKind.Open)]
    [DataRow("empire_s", "mordor", "empire_w", "gondor", GiftGateKind.Open)]
    [DataRow("empire_s", "mordor", "empire_s", "mordor", GiftGateKind.Open)]
    [DataRow("battania", "battania", "empire_s", "mordor", GiftGateKind.Open)]
    [DataRow("umbar", "umbar", "empire", "empire", GiftGateKind.Open)]
    [DataRow("empire_w", "gondor", "umbar", "umbar", GiftGateKind.Open)]
    [DataRow("empire_w", "gondor", "battania", "battania", GiftGateKind.Open)]
    [DataRow("battania", "battania", "battania", "battania", GiftGateKind.Open)]
    [DataRow("clan_test_1", "rivendell", "player_faction", "mordor", GiftGateKind.RefusesAlignment)]
    [DataRow("clan_test_1", "rivendell", "empire_s", "mordor", GiftGateKind.RefusesAlignment)]
    [DataRow("clan_test_1", "rivendell", "player_faction", "rivendell", GiftGateKind.Open)]
    [DataRow("clan_test_1", "vakken", "empire_s", "mordor", GiftGateKind.Open)]
    [DataRow("empire_w", "gondor", "player_faction", "vakken", GiftGateKind.Open)]
    public void Gate_AlignmentRefusal_IsFreeLordAgainstEvilPlayerOnly(string lordKingdom, string lordCulture,
        string playerKingdom, string playerCulture, GiftGateKind expected)
    {
        _f.Recipient = GiftFixture.Lord(kingdom: lordKingdom, culture: lordCulture, sideCulture: lordCulture);
        _f.Player = _f.Player! with { SideFactionId = playerKingdom, SideCultureId = playerCulture };

        Assert.AreEqual(expected, LordGate().Kind);
    }

    // The side is the realm's: the map faction with its own culture. The hero's own culture is only a taste, so each row gives
    // the lord an own culture that would flip the verdict if the gate read it. Recipient: faction, faction culture, own culture.
    [DataTestMethod]
    [DataRow("battania", "battania", "vlandia", "empire_s", "mordor", GiftGateKind.Open)]
    [DataRow("umbar", "umbar", "gondor", "empire_s", "mordor", GiftGateKind.Open)]
    [DataRow("vlandia", "vlandia", "battania", "empire_s", "mordor", GiftGateKind.RefusesAlignment)]
    public void Gate_AlignmentRefusal_ReadsTheRealmNotTheHerosOwnCulture(string lordFaction, string lordFactionCulture,
        string lordOwnCulture, string playerFaction, string playerFactionCulture, GiftGateKind expected)
    {
        _f.Recipient = GiftFixture.Lord(culture: lordOwnCulture, kingdom: lordFaction, sideCulture: lordFactionCulture);
        _f.Player = _f.Player! with
        {
            SideFactionId = playerFaction,
            SideCultureId = playerFactionCulture,
        };

        Assert.AreEqual(expected, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_AlignmentRefusalSwitchedOff_OpensTheFreeLordToTheEvilPlayer()
    {
        _f.Settings = _f.Settings with { AlignmentRefusalEnabled = false };
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };

        Assert.AreEqual(GiftGateKind.Open, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_FreeCompanionOfAnEvilPlayer_IsNotRefused()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };
        _f.Recipient = GiftFixture.Companion(culture: "gondor");

        Assert.AreEqual(GiftGateKind.Open, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_HostilityIsDecidedBeforeAlignment()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };
        _f.Recipient = GiftFixture.Lord(relation: -60);

        Assert.AreEqual(GiftGateKind.RefusesHostile, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_NeverChangesRelation()
    {
        _f.Recipient = GiftFixture.Lord(relation: -80);

        LordGate();

        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_HostileLord_DropsRelationByOneExactlyOnce()
    {
        _f.Recipient = GiftFixture.Lord(relation: -60);

        var gate = _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger);

        Assert.AreEqual(GiftGateKind.RefusesHostile, gate.Kind);
        _f.Relations.Received(1).Grant(GiftFixture.HeroId, -1, Arg.Any<string>());
        _f.Relations.ReceivedWithAnyArgs(1).Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_AlignmentRefusal_CostsNothing()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };

        var gate = _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger);

        Assert.AreEqual(GiftGateKind.RefusesAlignment, gate.Kind);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_OpenGate_CostsNothing()
    {
        Assert.AreEqual(GiftGateKind.Open, _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger).Kind);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_CooldownAndHiddenGates_CostNothing()
    {
        _f.Ledger.Stamp(GiftFixture.HeroId, 99);
        Assert.AreEqual(GiftGateKind.Cooldown, _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger).Kind);
        _f.Recipient = GiftFixture.Lord(relation: -80) with { IsAtWarWithPlayer = true };
        Assert.AreEqual(GiftGateKind.Hidden, _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger).Kind);

        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_HostileCompanion_CostsNothing()
    {
        _f.Recipient = GiftFixture.Companion(relation: -90);

        _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger);

        Assert.AreEqual(0, _f.Relations.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Gate_FeatureOff_ReadsNoFactsAtAll()
    {
        _f.Settings = _f.Settings with { Enabled = false };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);

        _f.Heroes.DidNotReceiveWithAnyArgs().ReadPlayer();
        _f.Heroes.DidNotReceiveWithAnyArgs().ReadRecipient(default!);
    }

    [TestMethod]
    public void Gate_PrisonerLord_IsHidden()
    {
        _f.Recipient = GiftFixture.Lord() with { IsPrisoner = true };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_PrisonerCompanion_IsHidden()
    {
        _f.Recipient = GiftFixture.Companion() with { IsPrisoner = true };

        Assert.AreEqual(GiftGateKind.Hidden, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_LordOfThePlayersOwnClan_IsHiddenOnTheLordBranch()
    {
        _f.Recipient = GiftFixture.Lord() with { IsInPlayerClan = true };

        Assert.AreEqual(GiftGateKind.Hidden, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_CompanionInThePlayersClan_KeepsTheCompanionBranch()
    {
        _f.Recipient = GiftFixture.Companion() with { IsInPlayerClan = true };

        Assert.AreEqual(GiftGateKind.Open, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Companion, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Gate_KinsmanOfAGiftedRelationOwner_SharesTheCooldown()
    {
        // With personal relations off a clan shares one relation, so a gift to the leader is a gift to his kin.
        _f.Ledger.Stamp("clan_leader", 98);
        _f.Recipient = GiftFixture.Lord(owner: "clan_leader");

        var gate = LordGate();

        Assert.AreEqual(GiftGateKind.Cooldown, gate.Kind);
        Assert.AreEqual(5, gate.DaysLeft);
    }

    [TestMethod]
    public void Gate_StampOnTheHeroHimself_DoesNotBlockAKinsmanWhoseOwnerIsAnother()
    {
        _f.Ledger.Stamp(GiftFixture.HeroId, 98);
        _f.Recipient = GiftFixture.Lord(owner: "clan_leader");

        Assert.AreEqual(GiftGateKind.Open, LordGate().Kind);
    }

    [TestMethod]
    public void Gate_BlankRelationOwner_FallsBackToTheHero()
    {
        _f.Ledger.Stamp(GiftFixture.HeroId, 98);
        _f.Recipient = GiftFixture.Lord(owner: "");

        Assert.AreEqual(GiftGateKind.Cooldown, LordGate().Kind);
    }

    [TestMethod]
    public void Ask_HostileLord_ShowsTheChangeThatLanded()
    {
        _f.Recipient = GiftFixture.Lord(relation: -60);
        _f.Relations.Grant(GiftFixture.HeroId, -1, Arg.Any<string>()).Returns(-1);

        _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger);

        _f.Heroes.Received(1).ShowRelationChange(GiftFixture.HeroId, -1);
        _f.Heroes.ReceivedWithAnyArgs(1).ShowRelationChange(default!, default);
    }

    [TestMethod]
    public void Ask_HostileLord_NothingLanded_ShowsNoNotice()
    {
        _f.Recipient = GiftFixture.Lord(relation: -60);
        _f.Relations.Grant(GiftFixture.HeroId, -1, Arg.Any<string>()).Returns(0);

        _f.Sut.Ask(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger);

        _f.Relations.Received(1).Grant(GiftFixture.HeroId, -1, Arg.Any<string>());
        _f.Heroes.DidNotReceiveWithAnyArgs().ShowRelationChange(default!, default);
    }
}
