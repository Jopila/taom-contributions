using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.LordCourtesies;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Tests.Features.LordCourtesies;

[TestClass]
public class LordCourtesiesServiceTests
{
    private LordCourtesiesFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new LordCourtesiesFixture();

    private static List<TroopCount> L(params TroopCount[] stacks) => stacks.ToList();

    // --- the side rule ---

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Free, true)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, true)]
    [DataRow(FactionSide.Free, FactionSide.Evil, false)]
    [DataRow(FactionSide.Evil, FactionSide.Free, false)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, true)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, true)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, true)]
    public void SidesAllowGift_RefusesOnlyFreeAgainstEvil(FactionSide player, FactionSide lord, bool allowed)
    {
        Assert.AreEqual(allowed, LordCourtesiesService.SidesAllowGift(player, lord));
    }

    // --- the reply routing ---

    [DataTestMethod]
    [DataRow(DomainOutcome.Unavailable, true)]
    [DataRow(DomainOutcome.Refused, true)]
    [DataRow(DomainOutcome.Revealed, false)]
    [DataRow(DomainOutcome.Landless, false)]
    public void RepliesWithRefusal_CoversEveryOutcome_SoTheReplyStateAlwaysHasALine(DomainOutcome outcome, bool refusal)
    {
        Assert.AreEqual(refusal, LordCourtesiesService.RepliesWithRefusal(outcome));
    }

    // --- gift eligibility ---

    [TestMethod]
    public void CanGift_AFreeWillingLordWithAParty_IsAllowed()
    {
        Assert.IsTrue(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_NoConversationPartner_IsRefused()
    {
        _f.Lords.ConversationPartnerId.Returns((string?)null);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_APrisonerOrDeadLord_IsRefused()
    {
        _f.Lords.Snapshot(LordCourtesiesFixture.LordId).Returns((LordCourtesySnapshot?)null);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ALordWithoutAParty_IsRefused()
    {
        _f.Lord.LeadsOwnParty = false;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ALordWhosePartyIsNotWithThePlayer_IsRefused()
    {
        _f.Lord.IsWithPlayer = false;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_APartyWithNoRoom_IsRefused()
    {
        _f.Lord.HasRoom = false;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ADisbandingParty_IsRefused()
    {
        _f.Lord.IsDisbanding = true;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_APlayerWithNoRegularTroops_IsRefused()
    {
        _f.Lord.PlayerHasTroops = false;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_APlayerClanLord_IsRefused()
    {
        _f.Lord.IsInPlayerClan = true;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ALordOfThePlayersKingdom_IsRefused()
    {
        _f.Lord.KingdomId = "k_player";

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_KinglessLordAndKinglessPlayer_IsNotTheSameKingdom()
    {
        _f.Lord.KingdomId = null;
        _f.Player.KingdomId.Returns((string?)null);

        Assert.IsTrue(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ALordAtWarWithThePlayer_IsRefused()
    {
        _f.Lord.AtWarWithPlayer = true;

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_FreePlayerEvilLord_IsRefused()
    {
        _f.Sides(FactionSide.Free, FactionSide.Evil);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_EvilPlayerFreeLord_IsRefused()
    {
        _f.Sides(FactionSide.Evil, FactionSide.Free);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ANeutralLord_MayReceive()
    {
        _f.Sides(FactionSide.Free, FactionSide.Neutral);

        Assert.IsTrue(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ReadsEachSideFromItsOwnIdAndCulturePair()
    {
        _f.Service.CanGift();

        _f.Alignment.Received().ResolveSide("k_player", "c_player");
        _f.Alignment.Received().ResolveSide("k_lord", "c_lord");
    }

    [TestMethod]
    public void CanGift_PlayerOfNeutralRealmWithFreeCulture_IsOfferedByEvilLord()
    {
        // #957: the player's realm is Neutral and the player's own culture is Free. The pair that sides the
        // player is the realm's id with the realm's culture, not the realm's id with the hero's culture.
        _f.Player.KingdomId.Returns("k_neutral_realm");
        _f.Player.CultureId.Returns("c_hero_free");
        _f.Player.Side.Returns(new SideIds("k_neutral_realm", "c_neutral_realm"));
        _f.Alignment.ResolveSide("k_neutral_realm", "c_hero_free").Returns(FactionSide.Free);
        _f.Alignment.ResolveSide("k_neutral_realm", "c_neutral_realm").Returns(FactionSide.Neutral);
        _f.Alignment.ResolveSide("k_lord", "c_lord").Returns(FactionSide.Evil);

        Assert.IsTrue(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_WhenTheHostOwnsTheWorld_IsHidden()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_OnADedicatedServer_IsHidden()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_WithTheToggleOff_IsHidden()
    {
        _f.Settings.TroopGiftsEnabled.Returns(false);

        Assert.IsFalse(_f.Service.CanGift());
    }

    [TestMethod]
    public void CanGift_ReadsTheSnapshotOnce()
    {
        _f.Service.CanGift();

        _f.Lords.Received(1).Snapshot(LordCourtesiesFixture.LordId);
    }

    // --- the gift flow ---

    [TestMethod]
    public void QueueGift_WhenIneligible_QueuesNothing()
    {
        _f.Lord.AtWarWithPlayer = true;

        _f.Service.QueueGift();

        _f.Lords.DidNotReceive().RunAfterConversation(Arg.Any<Action>());
    }

    [TestMethod]
    public void QueueGift_QueuesTheScreenForAfterTheConversation_AndOpensNothingYet()
    {
        _f.Service.QueueGift();

        Assert.IsNotNull(_f.Queued);
        _f.Lords.DidNotReceive().OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void QueueGift_ReadsTheSnapshotOnce()
    {
        _f.Service.QueueGift();

        _f.Lords.Received(1).Snapshot(LordCourtesiesFixture.LordId);
    }

    [TestMethod]
    public void TheQueuedScreen_SnapshotsTheRosterBeforeItOpens()
    {
        _f.Rosters(L(new TroopCount("a", 10)));
        _f.Service.QueueGift();

        _f.Queued!();

        Received.InOrder(() =>
        {
            _f.Lords.MemberTroops(LordCourtesiesFixture.LordId);
            _f.Lords.OpenGiftScreen(LordCourtesiesFixture.LordId, Arg.Any<Action>());
        });
    }

    [TestMethod]
    public void TheQueuedScreen_RechecksEligibility_AndDoesNotOpenForALordWhoBecameAPrisoner()
    {
        _f.Service.QueueGift();
        _f.Lords.Snapshot(LordCourtesiesFixture.LordId).Returns((LordCourtesySnapshot?)null);

        _f.Queued!();

        _f.Lords.DidNotReceive().OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void OpeningTheScreen_PaysNothing_UntilTheEngineCommits()
    {
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 25)));
        _f.Service.QueueGift();

        _f.Queued!();

        _f.Grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void ACommitWithDonatedTroops_PaysTheHandoverRuleOnTheEnginesInfluence()
    {
        // The engine's worth is 50: the hand-over rule gives min(10, 50 / 2) = 10 with a roll of 0.
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 25)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();

        _f.Lords.Received(1).GiftInfluence(Arg.Is<IReadOnlyList<TroopCount>>(d => d.Count == 1 && d[0].Count == 20));
        _f.Grants.Received(1).Grant(LordCourtesiesFixture.LordId, 10, Arg.Any<string>());
        _f.Notice.Received(1).ShowHandover("Lord One");
    }

    [TestMethod]
    public void ACommitWithNothingDonated_PaysNothing()
    {
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 5)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();

        _f.Lords.DidNotReceiveWithAnyArgs().GiftInfluence(default!);
        _f.Grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void AGiftTheEngineValuesUnderTwo_EarnsNothing()
    {
        _f.Lords.GiftInfluence(Arg.Any<IReadOnlyList<TroopCount>>()).Returns(1);
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 6)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();

        _f.Grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void ASecondCommit_PaysOnlyTheTroopsAddedSinceTheFirst()
    {
        _f.Lords.GiftInfluence(Arg.Any<IReadOnlyList<TroopCount>>()).Returns(6);
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 7)), L(new TroopCount("a", 10)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();
        _f.OnDone!();

        _f.Lords.Received(1).GiftInfluence(Arg.Is<IReadOnlyList<TroopCount>>(d => d.Count == 1 && d[0].Count == 2));
        _f.Lords.Received(1).GiftInfluence(Arg.Is<IReadOnlyList<TroopCount>>(d => d.Count == 1 && d[0].Count == 3));
        _f.Grants.Received(2).Grant(LordCourtesiesFixture.LordId, 3, Arg.Any<string>());
    }

    [TestMethod]
    public void ARepeatedCommitWithNoNewTroops_PaysNothingAgain()
    {
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 25)), L(new TroopCount("a", 25)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();
        _f.OnDone!();

        _f.Grants.Received(1).Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [TestMethod]
    public void TwoGiftsTheSameDay_ShareTheHandoversDailyCap()
    {
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 25)), L(new TroopCount("a", 45)));
        _f.Service.QueueGift();
        _f.Queued!();

        _f.OnDone!();
        _f.OnDone!();

        _f.Grants.Received(1).Grant(LordCourtesiesFixture.LordId, 10, Arg.Any<string>());
        _f.Grants.Received(1).Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [TestMethod]
    public void TheCapIsKeyedOnTheRelationOwner_SoKinsmenShareOneDay()
    {
        // A second lord whose relation lands on the same owner draws on the same 10 points.
        _f.Rosters(L(new TroopCount("a", 5)), L(new TroopCount("a", 25)));
        _f.Service.QueueGift();
        _f.Queued!();
        _f.OnDone!();

        var paid = _f.Handover.OnHandover("lord2", "Lord Two", 50, false, false, LordCourtesiesFixture.OwnerId);

        Assert.IsFalse(paid, "the owner already received 10 today");
    }

    // --- the toggles and the gates, at every stage ---

    [TestMethod]
    public void QueueGift_WithTheToggleOff_QueuesNothing()
    {
        _f.Settings.TroopGiftsEnabled.Returns(false);

        _f.Service.QueueGift();

        _f.Lords.DidNotReceive().RunAfterConversation(Arg.Any<Action>());
    }

    [TestMethod]
    public void AskDomain_WithTheToggleOff_IsUnavailableAndDoesNothing()
    {
        _f.Settings.DomainRevealEnabled.Returns(false);

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Unavailable, outcome);
        _f.Reveal.DidNotReceive().RevealClan(Arg.Any<string>());
        _f.Player.DidNotReceive().ChangeRelationWith(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TheQueuedScreen_RechecksTheToggle_WhenItOpens()
    {
        _f.Service.QueueGift();
        _f.Settings.TroopGiftsEnabled.Returns(false);

        _f.Queued!();

        _f.Lords.DidNotReceive().OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void TheQueuedScreen_RechecksTheCoopGate_WhenItOpens()
    {
        _f.Service.QueueGift();
        _f.Coop.ShouldDeferToHost.Returns(true);

        _f.Queued!();

        _f.Lords.DidNotReceive().OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void TheQueuedScreen_RechecksTheDedicatedServerGate_WhenItOpens()
    {
        _f.Service.QueueGift();
        _f.Server.IsDedicatedServer.Returns(true);

        _f.Queued!();

        _f.Lords.DidNotReceive().OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>());
    }

    // --- domain reveal: eligibility ---

    [TestMethod]
    public void CanAskDomain_AFreeLordNotAtWar_IsAllowed()
    {
        Assert.IsTrue(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_AMessengerReachedLordFarFromThePlayer_IsStillAllowed()
    {
        _f.Lord.IsWithPlayer = false;

        Assert.IsTrue(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_AnUnwelcomeLordStillGetsTheLine_SoTheOutcomeDecidesNotTheMenu()
    {
        _f.Lord.Relation = -50;

        Assert.IsTrue(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_APrisonerOrDeadLord_IsHidden()
    {
        _f.Lords.Snapshot(LordCourtesiesFixture.LordId).Returns((LordCourtesySnapshot?)null);

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_NoConversationPartner_IsHidden()
    {
        _f.Lords.ConversationPartnerId.Returns((string?)null);

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_ALordAtWarWithThePlayer_IsHidden()
    {
        _f.Lord.AtWarWithPlayer = true;

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_APlayerClanLord_IsHidden()
    {
        _f.Lord.IsInPlayerClan = true;

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_AClanlessLord_IsHidden()
    {
        _f.Lord.ClanId = null;

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_WhenTheHostOwnsTheWorld_IsHidden()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_OnADedicatedServer_IsHidden()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_WithTheToggleOff_IsHidden()
    {
        _f.Settings.DomainRevealEnabled.Returns(false);

        Assert.IsFalse(_f.Service.CanAskDomain());
    }

    [TestMethod]
    public void CanAskDomain_ReadsTheSnapshotOnce()
    {
        _f.Service.CanAskDomain();

        _f.Lords.Received(1).Snapshot(LordCourtesiesFixture.LordId);
    }

    [TestMethod]
    public void AskDomain_ReadsTheSnapshotOnce()
    {
        _f.Service.AskDomain();

        _f.Lords.Received(1).Snapshot(LordCourtesiesFixture.LordId);
    }

    [TestMethod]
    public void TheTwoToggles_AreIndependent()
    {
        _f.Settings.TroopGiftsEnabled.Returns(false);
        Assert.IsTrue(_f.Service.CanAskDomain());

        _f.Settings.TroopGiftsEnabled.Returns(true);
        _f.Settings.DomainRevealEnabled.Returns(false);
        Assert.IsTrue(_f.Service.CanGift());
    }

    // --- domain reveal: outcomes ---

    [TestMethod]
    public void AskDomain_AtOrAboveTheThreshold_RevealsTheClan_AndCountsWhatTheRevealMarked()
    {
        _f.Lord.Relation = 5;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Revealed, outcome);
        _f.Reveal.Received(1).RevealClan("clan_lord1");
        _f.Lords.Received(1).ShowHoldingsMarked(3);
        _f.Player.DidNotReceive().ChangeRelationWith(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_AClanOfVillagesOnly_RevealsButShowsNoCount()
    {
        _f.Lord.Holdings = 2;
        _f.Lord.Fortifications = 0;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Revealed, outcome);
        _f.Lords.DidNotReceive().ShowHoldingsMarked(Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_BelowTheThreshold_RefusesAndCostsRelation()
    {
        _f.Lord.Relation = 4;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Refused, outcome);
        _f.Player.Received(1).ChangeRelationWith(LordCourtesiesFixture.LordId, -1);
        _f.Reveal.DidNotReceive().RevealClan(Arg.Any<string>());
        _f.Lords.DidNotReceive().ShowHoldingsMarked(Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_ALandlessClan_CostsRelationAndRevealsNothing()
    {
        _f.Lord.Holdings = 0;
        _f.Lord.Fortifications = 0;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Landless, outcome);
        _f.Player.Received(1).ChangeRelationWith(LordCourtesiesFixture.LordId, -5);
        _f.Reveal.DidNotReceive().RevealClan(Arg.Any<string>());
        _f.Lords.DidNotReceive().ShowHoldingsMarked(Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_AnUnwelcomeLandlessLord_RefusesFirst()
    {
        _f.Lord.Relation = 0;
        _f.Lord.Holdings = 0;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Refused, outcome);
        _f.Player.Received(1).ChangeRelationWith(LordCourtesiesFixture.LordId, -1);
    }

    [TestMethod]
    public void AskDomain_ACostOfZero_ChangesNoRelation()
    {
        _f.Config.RefusalCost = 0f;
        _f.Lord.Relation = 0;

        _f.Service.AskDomain();

        _f.Player.DidNotReceive().ChangeRelationWith(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_FollowsTheConfig()
    {
        _f.Config.RevealMinRelation = 20f;
        _f.Config.RefusalCost = 3f;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Refused, outcome);
        _f.Player.Received(1).ChangeRelationWith(LordCourtesiesFixture.LordId, -3);
    }

    [TestMethod]
    public void AskDomain_WhenNotAllowed_IsUnavailableAndDoesNothing()
    {
        _f.Lord.AtWarWithPlayer = true;

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Unavailable, outcome);
        _f.Reveal.DidNotReceive().RevealClan(Arg.Any<string>());
        _f.Player.DidNotReceive().ChangeRelationWith(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void AskDomain_AnEngineFaultInTheReveal_IsLoggedAndTheConversationGoesOn()
    {
        _f.Reveal.When(r => r.RevealClan(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("tracker gone"));

        var outcome = _f.Service.AskDomain();

        Assert.AreEqual(DomainOutcome.Unavailable, outcome);
        _f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("tracker gone")));
    }
}
