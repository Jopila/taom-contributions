using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The act of giving: what leaves the player, what the relation grant is asked for and what comes back. The option handed in
/// is only a pointer; the service rebuilds the list from fresh facts, so a stale option or a changed relation cannot slip
/// through.
/// </summary>
[TestClass]
public class GiftServiceGiveTests
{
    private GiftFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GiftFixture();

    private GiftOption Pick(GiftEntry entry, string itemId) =>
        _f.Sut.Options(GiftFixture.HeroId, entry).Single(o => o.ItemId == itemId);

    private GiftOption PickPurse(int amount) =>
        _f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion).Single(o => o.Kind == GiftOptionKind.Purse && o.Amount == amount);

    private GiftOutcome GiveGood(GiftEntry entry, string itemId, int? lastDay = null)
    {
        if (lastDay is { } day)
            _f.Ledger.Stamp(GiftFixture.HeroId, day);
        return _f.Sut.Give(GiftFixture.HeroId, entry, Pick(entry, itemId), _f.Ledger);
    }

    [TestMethod]
    public void Give_Good_TakesOneFromTheStackAndGrantsTheGain()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 3));

        var outcome = GiveGood(GiftEntry.Lord, "ring");

        Assert.AreEqual(GiftResult.Given, outcome.Result);
        _f.Roster.Received(1).RemoveItem("ring", 1);
        _f.Roster.ReceivedWithAnyArgs(1).RemoveItem(default!, default);
        _f.Relations.Received(1).Grant(GiftFixture.HeroId, 8, Arg.Any<string>());
        _f.Relations.ReceivedWithAnyArgs(1).Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_Good_ReportsTheLandedChangeAndTheTaste()
    {
        _f.Recipient = GiftFixture.Lord(race: "dwarf");
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _f.Relations.Grant(GiftFixture.HeroId, 12, Arg.Any<string>()).Returns(11);

        var outcome = GiveGood(GiftEntry.Lord, "ring");

        Assert.AreEqual(new GiftOutcome(GiftResult.Given, true, 11), outcome);
    }

    [TestMethod]
    public void Give_Good_NotToTheHeroesTaste_IsNotSuited()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        Assert.IsFalse(GiveGood(GiftEntry.Lord, "ring").Suited);
    }

    [TestMethod]
    public void Give_Good_CommitsTheOpenPartyScreenAfterTheItemLeaves()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var order = new List<string>();
        _f.Roster.When(r => r.RemoveItem(Arg.Any<string>(), Arg.Any<int>())).Do(_ => order.Add("remove"));
        _f.Items.When(i => i.CommitOpenPartyScreen()).Do(_ => order.Add("commit"));

        GiveGood(GiftEntry.Lord, "ring");

        CollectionAssert.AreEqual(new[] { "remove", "commit" }, order);
    }

    [TestMethod]
    public void Give_Good_StackAlreadyGone_FailsWithoutAGrantOrACommit()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Roster.RemoveItem("ring", 1).Returns(0);

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _f.Items.DidNotReceive().CommitOpenPartyScreen();
    }

    [TestMethod]
    public void Give_Good_NoLongerHeld_FailsAndTakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Held.Clear();

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_UsesTheRelationOfNow_NotTheRelationWhenTheListWasBuilt()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        Assert.AreEqual(8, option.Gain);
        _f.Recipient = GiftFixture.Lord(relation: 45);

        _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        _f.Relations.Received(1).Grant(GiftFixture.HeroId, 4, Arg.Any<string>());
    }

    [TestMethod]
    public void Give_Purse_MovesTheGoldAndNeverTouchesTheInventory()
    {
        _f.Recipient = GiftFixture.Companion();

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, PickPurse(2000), _f.Ledger);

        Assert.AreEqual(GiftResult.Given, outcome.Result);
        _f.Heroes.Received(1).PayGoldToHero(GiftFixture.HeroId, 2000);
        _f.Heroes.ReceivedWithAnyArgs(1).PayGoldToHero(default!, default);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        _f.Relations.Received(1).Grant(GiftFixture.HeroId, 4, Arg.Any<string>());
    }

    [TestMethod]
    public void Give_Purse_GoldShortSinceTheListWasBuilt_FailsWithoutPayingOrGranting()
    {
        _f.Recipient = GiftFixture.Companion();
        var option = PickPurse(2000);
        _f.Player = _f.Player! with { Gold = 1999 };

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Heroes.DidNotReceiveWithAnyArgs().PayGoldToHero(default!, default);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_Purse_ABlockedOption_FailsWithoutPaying()
    {
        _f.Recipient = GiftFixture.Companion();
        _f.Player = _f.Player! with { Gold = 700 };

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, PickPurse(2000), _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Heroes.DidNotReceiveWithAnyArgs().PayGoldToHero(default!, default);
    }

    [TestMethod]
    public void Give_Purse_TheTransferFails_NoGrant()
    {
        _f.Recipient = GiftFixture.Companion();
        _f.Heroes.PayGoldToHero(GiftFixture.HeroId, 500).Returns(false);

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, PickPurse(500), _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_Purse_ToALord_Fails()
    {
        _f.Recipient = GiftFixture.Companion();
        var purse = PickPurse(500);
        _f.Recipient = GiftFixture.Lord();

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, purse, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Heroes.DidNotReceiveWithAnyArgs().PayGoldToHero(default!, default);
    }

    [TestMethod]
    public void Give_NoEffectGift_IsNotTaken()
    {
        _f.Recipient = GiftFixture.Lord(relation: 90);
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        Assert.AreEqual(GiftBlock.NoEffect, option.Block);

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_InsideTheCooldown_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        var outcome = GiveGood(GiftEntry.Lord, "ring", lastDay: 98);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_ToALordAtWar_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Recipient = GiftFixture.Lord() with { IsAtWarWithPlayer = true };

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_ToAHostileLord_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Recipient = GiftFixture.Lord(relation: -70);

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_FreeLordFromAnEvilPlayer_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Player = _f.Player! with { KingdomId = "mordor", CultureId = "mordor" };

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_FeatureSwitchedOffMidConversation_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Settings = _f.Settings with { Enabled = false };

        Assert.AreEqual(GiftResult.Failed, _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger).Result);
    }

    [TestMethod]
    public void Give_LandedChangeIsNonZero_ShowsTheNoticeOnceWithWhatLanded()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _f.Relations.Grant(GiftFixture.HeroId, 8, Arg.Any<string>()).Returns(3);

        GiveGood(GiftEntry.Lord, "ring");

        _f.Heroes.Received(1).ShowRelationChange(GiftFixture.HeroId, 3);
        _f.Heroes.ReceivedWithAnyArgs(1).ShowRelationChange(default!, default);
    }

    [TestMethod]
    public void Give_NothingLanded_ShowsNoRelationText_ButStillCountsAsGiven()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _f.Relations.Grant(GiftFixture.HeroId, 8, Arg.Any<string>()).Returns(0);

        var outcome = GiveGood(GiftEntry.Lord, "ring");

        Assert.AreEqual(GiftResult.Given, outcome.Result);
        Assert.AreEqual(0, outcome.Landed);
        _f.Heroes.DidNotReceiveWithAnyArgs().ShowRelationChange(default!, default);
    }

    [TestMethod]
    public void Give_ThroughTheRealRelationGrantService_ClampsAndReportsWhatLanded()
    {
        var engine = Substitute.For<IHeroRelationAdapter>();
        engine.ApplyPlayerRelation(GiftFixture.HeroId, Arg.Any<int>()).Returns(call => call.ArgAt<int>(1) - 2);
        var server = Substitute.For<IDedicatedServerProvider>();
        var grants = new RelationGrantService(engine, server, Substitute.For<IModLogger>());
        var sut = new GiftService(_f.Heroes, _f.Items, _f.Roster, _f.CatalogProvider, _f.SettingsProvider, _f.Alignment, grants, _f.Logger);
        _f.Recipient = GiftFixture.Lord(culture: "vlandia");
        _f.Held.Add(GiftFixture.Good("great_steed", "noble_horse", 5000));
        var option = sut.Options(GiftFixture.HeroId, GiftEntry.Lord).Single();

        var outcome = sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        engine.Received(1).ApplyPlayerRelation(GiftFixture.HeroId, 15);
        Assert.AreEqual(13, outcome.Landed);
    }

    [TestMethod]
    public void Give_ThroughTheRealRelationGrantService_OnADedicatedServer_LandsNothing_ButStillTakesTheGift()
    {
        var server = Substitute.For<IDedicatedServerProvider>();
        server.IsDedicatedServer.Returns(true);
        var grants = new RelationGrantService(Substitute.For<IHeroRelationAdapter>(), server, Substitute.For<IModLogger>());
        var sut = new GiftService(_f.Heroes, _f.Items, _f.Roster, _f.CatalogProvider, _f.SettingsProvider, _f.Alignment, grants, _f.Logger);
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = sut.Options(GiftFixture.HeroId, GiftEntry.Lord).Single();

        var outcome = sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger);

        Assert.AreEqual(GiftResult.Given, outcome.Result);
        Assert.AreEqual(0, outcome.Landed);
        _f.Roster.Received(1).RemoveItem("ring", 1);
        _f.Heroes.DidNotReceiveWithAnyArgs().ShowRelationChange(default!, default);
    }

    [TestMethod]
    public void Give_AFinishedGift_StampsTheLedgerOnTheRelationOwnerFromToday()
    {
        _f.Player = _f.Player! with { Today = 321 };
        _f.Recipient = GiftFixture.Lord(owner: "clan_leader");
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        GiveGood(GiftEntry.Lord, "ring");

        Assert.AreEqual(321, _f.Ledger.LastGiftDay("clan_leader"));
        Assert.IsNull(_f.Ledger.LastGiftDay(GiftFixture.HeroId), "the key is the relation owner, not the hero");
        Assert.AreEqual(1, _f.Ledger.Count);
    }

    [TestMethod]
    public void Give_APurse_StampsTheLedgerToo()
    {
        _f.Player = _f.Player! with { Today = 40 };
        _f.Recipient = GiftFixture.Companion();

        _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, PickPurse(500), _f.Ledger);

        Assert.AreEqual(40, _f.Ledger.LastGiftDay(GiftFixture.HeroId));
    }

    [TestMethod]
    public void Give_TwoKinsmenOfOneRelationOwner_ShareOneCooldown()
    {
        // Personal Relations off: a clan shares one relation, so a second kinsman cannot be gifted straight after the first.
        const string kin = "kinsman_of_the_lord";
        _f.Recipient = GiftFixture.Lord(owner: "clan_leader");
        _f.Heroes.ReadRecipient(kin).Returns(GiftFixture.Lord(owner: "clan_leader") with { HeroId = kin });
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 3));

        Assert.AreEqual(GiftResult.Given, GiveGood(GiftEntry.Lord, "ring").Result);

        Assert.AreEqual(GiftGateKind.Cooldown, _f.Sut.Gate(kin, GiftEntry.Lord, _f.Ledger).Kind);
        Assert.AreEqual(GiftGateKind.Cooldown, _f.Sut.Gate(GiftFixture.HeroId, GiftEntry.Lord, _f.Ledger).Kind);
    }

    [TestMethod]
    public void Give_WhenNothingChangedHands_StartsNoCooldown()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Roster.RemoveItem("ring", 1).Returns(0);

        Assert.AreEqual(GiftResult.Failed, _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger).Result);

        Assert.AreEqual(0, _f.Ledger.Count);
    }

    [TestMethod]
    public void Give_APurseThatCouldNotBePaid_StartsNoCooldown()
    {
        _f.Recipient = GiftFixture.Companion();
        _f.Heroes.PayGoldToHero(GiftFixture.HeroId, 500).Returns(false);

        _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Companion, PickPurse(500), _f.Ledger);

        Assert.AreEqual(0, _f.Ledger.Count);
    }

    [TestMethod]
    public void Give_NoOption_Fails()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        var outcome = _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, null!, _f.Ledger);

        Assert.AreEqual(GiftResult.Failed, outcome.Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Give_NoPlayerFacts_Fails()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Player = null;

        Assert.AreEqual(GiftResult.Failed, _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger).Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_UnknownRecipient_Fails()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Recipient = null;

        Assert.AreEqual(GiftResult.Failed, _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger).Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Give_ModifiedStackOnly_Fails()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        var option = Pick(GiftEntry.Lord, "ring");
        _f.Held.Clear();
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 1, modified: true));

        Assert.AreEqual(GiftResult.Failed, _f.Sut.Give(GiftFixture.HeroId, GiftEntry.Lord, option, _f.Ledger).Result);
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }
}
