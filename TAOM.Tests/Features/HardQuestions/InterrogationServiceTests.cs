using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Tests.Features.HardQuestions;

[TestClass]
public class InterrogationServiceTests
{
    private HardQuestionsFixture _f = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new HardQuestionsFixture();
    }

    // --- eligibility ---

    [TestMethod]
    public void CanAsk_ACaptiveHeldByThePlayersClan_IsTrueForBothTopics()
    {
        _f.Captive();

        Assert.IsTrue(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
        Assert.IsTrue(_f.Service.CanAsk(HardQuestionTopic.Kingdom, "lord1"));
        Assert.IsTrue(_f.Service.CanStrip("lord1"));
    }

    [TestMethod]
    public void CanAsk_NoPartnerId_IsFalse()
    {
        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, null));
        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, ""));
        Assert.IsFalse(_f.Service.CanStrip(null));
    }

    [TestMethod]
    public void CanAsk_FeatureOff_IsFalse()
    {
        _f.Captive();
        _f.Settings.IsEnabled.Returns(false);

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
        Assert.IsFalse(_f.Service.CanStrip("lord1"));
    }

    [TestMethod]
    public void CanAsk_WhenTheModMustDeferToACoopHost_IsFalse()
    {
        _f.Captive();
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
        Assert.IsFalse(_f.Service.CanStrip("lord1"));
    }

    [TestMethod]
    public void CanAsk_AuthorityTrueButDeferTrue_IsStillFalse()
    {
        _f.Captive();
        _f.Coop.IsAuthority.Returns(true);
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Kingdom, "lord1"));
        _f.Captives.DidNotReceiveWithAnyArgs().Snapshot(default!);
    }

    [TestMethod]
    public void CanAsk_InAPrisonerDecisionConversation_IsFalse()
    {
        _f.Captive();
        _f.Captives.IsPrisonerDecisionContext.Returns(true);

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
        Assert.IsFalse(_f.Service.CanStrip("lord1"));
    }

    [TestMethod]
    public void CanAsk_NotACaptive_IsFalse()
    {
        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "nobody"));
    }

    [TestMethod]
    public void CanAsk_HeldByAnotherClan_IsFalse()
    {
        _f.Captive().CaptorIsPlayerClan = false;

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void CanAsk_ALordOfThePlayersOwnClan_IsFalse()
    {
        _f.Captive().IsInPlayerClan = true;

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void CanAsk_ATopicAlreadyUsed_IsFalseForThatTopicOnly()
    {
        _f.Captive();
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
        Assert.IsTrue(_f.Service.CanAsk(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void CanStrip_AlreadyStripped_IsFalse()
    {
        _f.Captive();
        _f.State.MarkStripped("lord1");

        Assert.IsFalse(_f.Service.CanStrip("lord1"));
    }

    // --- price ---

    [TestMethod]
    public void Quote_IsTheRansomTimesTheTopicMultiplier()
    {
        _f.Captive(ransom: 1000);

        Assert.AreEqual(1500, _f.Service.Quote(HardQuestionTopic.Clan, "lord1"));
        Assert.AreEqual(3500, _f.Service.Quote(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void Quote_ACalculatingLordTakesHalf()
    {
        _f.Captive(ransom: 1000).CalculatingLevel = 2;

        Assert.AreEqual(750, _f.Service.Quote(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void Quote_NeverBelowOneGold()
    {
        _f.Captive(ransom: 0);

        Assert.AreEqual(1, _f.Service.Quote(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void Quote_AHugeRansom_IsClampedBelowIntMax()
    {
        _f.Captive(ransom: int.MaxValue);

        Assert.AreEqual(int.MaxValue / 2, _f.Service.Quote(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void Quote_NegativeRansom_IsTreatedAsZero()
    {
        _f.Captive(ransom: -50);

        Assert.AreEqual(1, _f.Service.Quote(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void Quote_IneligibleLord_IsZero()
    {
        Assert.AreEqual(0, _f.Service.Quote(HardQuestionTopic.Clan, "nobody"));
    }

    [TestMethod]
    public void CanAfford_ComparesTheQuoteToTheGold()
    {
        _f.Captive(ransom: 1000);
        _f.Player.Gold.Returns(1500);
        Assert.IsTrue(_f.Service.CanAfford(HardQuestionTopic.Clan, "lord1"));

        _f.Player.Gold.Returns(1499);
        Assert.IsFalse(_f.Service.CanAfford(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void CanAfford_IneligibleLord_IsFalse()
    {
        Assert.IsFalse(_f.Service.CanAfford(HardQuestionTopic.Clan, "nobody"));
    }

    // --- reply ---

    [TestMethod]
    public void ReplyKind_FollowsTheLordsTemperament()
    {
        var snapshot = _f.Captive();
        Assert.AreEqual(BribeReplyKind.Plain, _f.Service.ReplyKind("lord1"));

        snapshot.ValorLevel = 1;
        Assert.AreEqual(BribeReplyKind.Valor, _f.Service.ReplyKind("lord1"));

        snapshot.CalculatingLevel = 1;
        Assert.AreEqual(BribeReplyKind.Calculating, _f.Service.ReplyKind("lord1"));
    }

    [TestMethod]
    public void ReplyKind_IneligibleLord_IsPlain()
    {
        Assert.AreEqual(BribeReplyKind.Plain, _f.Service.ReplyKind("nobody"));
    }

    // --- bribe ---

    [TestMethod]
    public void PayBribe_Clan_PaysTheLordGivesCharmAndRevealsTheClan()
    {
        _f.Captive(ransom: 1000);

        Assert.IsTrue(_f.Service.PayBribe(HardQuestionTopic.Clan, "lord1"));

        _f.Actions.Received().PayGold("lord1", 1500);
        _f.Skills.Received().AddSkillXp("main_hero", "Charm", 50f);
        _f.Reveal.Received().RevealClan("clan_lord1");
        _f.Reveal.DidNotReceive().RevealKingdom(Arg.Any<string>());
        Assert.IsTrue(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void PayBribe_Kingdom_PaysMoreGivesMoreCharmAndRevealsTheKingdom()
    {
        _f.Captive(ransom: 1000);

        Assert.IsTrue(_f.Service.PayBribe(HardQuestionTopic.Kingdom, "lord1"));

        _f.Actions.Received().PayGold("lord1", 3500);
        _f.Skills.Received().AddSkillXp("main_hero", "Charm", 90f);
        _f.Reveal.Received().RevealKingdom("k_lord1");
    }

    [TestMethod]
    public void CanAsk_KingdomTopicOfAKinglessLord_IsFalseButTheClanTopicStays()
    {
        _f.Captive(kingdom: null);

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Kingdom, "lord1"));
        Assert.IsTrue(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));
    }

    [TestMethod]
    public void PayBribeAndQueueForce_KingdomTopicOfAKinglessLord_DoNothing()
    {
        _f.Captive(kingdom: null);

        Assert.IsFalse(_f.Service.PayBribe(HardQuestionTopic.Kingdom, "lord1"));
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");
        _f.Service.CompletePending();

        _f.Actions.DidNotReceiveWithAnyArgs().PayGold(default!, default);
        _f.Actions.DidNotReceive().CommitOpenPartyScreen();
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void PayBribe_ALordWithNoClan_RevealsNothing()
    {
        _f.Captive(kingdom: null).ClanId = null;

        Assert.IsTrue(_f.Service.PayBribe(HardQuestionTopic.Clan, "lord1"));

        _f.Reveal.DidNotReceiveWithAnyArgs().RevealClan(default!);
        _f.Reveal.DidNotReceiveWithAnyArgs().RevealKingdom(default!);
    }

    [TestMethod]
    public void PayBribe_WhenThePaymentFails_ChangesNothing()
    {
        _f.Captive();
        _f.Actions.PayGold(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        Assert.IsFalse(_f.Service.PayBribe(HardQuestionTopic.Clan, "lord1"));

        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        _f.Skills.DidNotReceiveWithAnyArgs().AddSkillXp(default!, default!, default);
        _f.Reveal.DidNotReceiveWithAnyArgs().RevealClan(default!);
    }

    [TestMethod]
    public void PayBribe_IneligibleOrAlreadyAsked_DoesNothing()
    {
        Assert.IsFalse(_f.Service.PayBribe(HardQuestionTopic.Clan, "nobody"));

        _f.Captive();
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");
        Assert.IsFalse(_f.Service.PayBribe(HardQuestionTopic.Clan, "lord1"));

        _f.Actions.DidNotReceiveWithAnyArgs().PayGold(default!, default);
    }

    [TestMethod]
    public void PayBribe_PaidInGold_ShowsNoRelationSummary()
    {
        _f.Captive();

        var paid = _f.Service.PayBribe(HardQuestionTopic.Clan, "lord1");

        Assert.IsTrue(paid);
        _f.Actions.DidNotReceiveWithAnyArgs().ShowRelationsHurt(default);
    }

    // --- force ---

    [TestMethod]
    public void QueueForce_MarksTheTopicButChangesNothingUntilTheConversationEnds()
    {
        _f.Captive();

        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        Assert.IsTrue(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        _f.Actions.DidNotReceiveWithAnyArgs().ChangeRelation(default!, default);
        _f.Reveal.DidNotReceiveWithAnyArgs().RevealClan(default!);
    }

    [TestMethod]
    public void QueueForce_IneligibleOrAlreadyAsked_QueuesNothing()
    {
        _f.Service.QueueForce(HardQuestionTopic.Clan, "nobody");
        _f.Captive();
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Traits.DidNotReceiveWithAnyArgs().AddTraitXp(default!, default);
        _f.Actions.DidNotReceive().CommitOpenPartyScreen();
    }

    [TestMethod]
    public void CompletePending_ForcedClanQuestion_ChargesRelationTraitsAndRevealsTheClan()
    {
        _f.Captive();
        _f.Circle.Family = HardQuestionsFixture.Lords("wife", "son");
        _f.Circle.KingdomLeader = HardQuestionsFixture.Lord("king");
        _f.Circle.Companions = new List<CompanionRef> { new("merciful", 2, 0), new("hard", 0, 2), new("gentle", 1, 0) };
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received().ChangeRelation("lord1", -20);
        _f.Actions.Received().ChangeRelation("wife", -10);
        _f.Actions.Received().ChangeRelation("son", -10);
        _f.Actions.DidNotReceive().ChangeRelation("king", Arg.Any<int>());
        _f.Actions.Received().ChangeRelation("merciful", -2);
        _f.Actions.Received().ChangeRelation("gentle", -1);
        _f.Actions.DidNotReceive().ChangeRelation("hard", Arg.Any<int>());
        _f.Traits.Received().AddTraitXp("Mercy", -160);
        _f.Traits.Received().AddTraitXp("Honor", -100);
        _f.Skills.Received().AddSkillXp("main_hero", "Roguery", 80f);
        _f.Reveal.Received().RevealClan("clan_lord1");
        _f.Actions.DidNotReceive().Wound(Arg.Any<string>());
        _f.Actions.DidNotReceive().KillByWounds(Arg.Any<string>());
    }

    [TestMethod]
    public void CompletePending_ForcedKingdomQuestion_ChargesTheLargerCostsAndTheKing()
    {
        _f.Captive();
        _f.Circle.Family = HardQuestionsFixture.Lords("wife");
        _f.Circle.KingdomLeader = HardQuestionsFixture.Lord("king");
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received().ChangeRelation("lord1", -40);
        _f.Actions.Received().ChangeRelation("wife", -20);
        _f.Actions.Received().ChangeRelation("king", -16);
        _f.Traits.Received().AddTraitXp("Mercy", -300);
        _f.Traits.Received().AddTraitXp("Honor", -200);
        _f.Skills.Received().AddSkillXp("main_hero", "Roguery", 150f);
        _f.Reveal.Received().RevealKingdom("k_lord1");
    }

    [TestMethod]
    public void CompletePending_ForcedQuestionWithoutKin_ChargesOnlyTheLord()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CompletePending_ForcingANonLeaderLord_ChargesHisLeadersIdOnceAtTheStrongestDelta()
    {
        _f.Captive(relationId: "chief");
        _f.Circle.Family = new List<LordRef> { HardQuestionsFixture.Lord("brother", "chief") };
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received(1).ChangeRelation("chief", -20);
        _f.Actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CompletePending_AKingdomTopicWhoseLordLostHisKingdomMeanwhile_RevealsNothing()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");
        _f.Captives.Circle("lord1").Returns(new CaptiveCircle(new LordRef("lord1", null, "c_lord1")));

        _f.Service.CompletePending();

        _f.Reveal.DidNotReceiveWithAnyArgs().RevealKingdom(default!);
    }

    [TestMethod]
    public void CompletePending_AHealthyLordRollingUnderTheWoundChance_IsWounded()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.14);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received().Wound("lord1");
        _f.Reveal.Received().RevealClan("clan_lord1");
    }

    [TestMethod]
    public void CompletePending_AHealthyLordRollingOverTheWoundChance_IsNotWounded()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.16);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.DidNotReceive().Wound(Arg.Any<string>());
    }

    [TestMethod]
    public void CompletePending_AWoundedLordOnTheKingdomTopicRollingUnderTheDeathChance_DiesAndRevealsNothing()
    {
        _f.Captive().IsWounded = true;
        _f.Random.NextDouble().Returns(0.04);
        _f.Actions.KillByWounds("lord1").Returns(true);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received().KillByWounds("lord1");
        _f.Traits.Received(2).AddTraitXp("Honor", -200);
        _f.Reveal.DidNotReceiveWithAnyArgs().RevealKingdom(default!);
        _f.Reveal.DidNotReceiveWithAnyArgs().RevealClan(default!);
    }

    [TestMethod]
    public void CompletePending_WhenTheEngineRefusesTheDeath_RevealsAndChargesNoExtraHonor()
    {
        _f.Captive().IsWounded = true;
        _f.Random.NextDouble().Returns(0.04);
        _f.Actions.KillByWounds("lord1").Returns(false);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Service.CompletePending();

        _f.Traits.Received(1).AddTraitXp("Honor", -200);
        _f.Reveal.Received().RevealKingdom("k_lord1");
        _f.Actions.DidNotReceive().Wound(Arg.Any<string>());
    }

    [TestMethod]
    public void CompletePending_AWoundedLordOnTheKingdomTopicSurviving_RevealsAndIsNotWoundedAgain()
    {
        _f.Captive().IsWounded = true;
        _f.Random.NextDouble().Returns(0.06);
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Service.CompletePending();

        _f.Actions.DidNotReceive().KillByWounds(Arg.Any<string>());
        _f.Actions.DidNotReceive().Wound(Arg.Any<string>());
        _f.Reveal.Received().RevealKingdom("k_lord1");
        _f.Traits.Received().AddTraitXp("Honor", -200);
    }

    [TestMethod]
    public void CompletePending_AWoundedLordOnTheClanTopic_NeverDies()
    {
        _f.Captive().IsWounded = true;
        _f.Random.NextDouble().Returns(0.0);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.DidNotReceive().KillByWounds(Arg.Any<string>());
        _f.Actions.DidNotReceive().Wound(Arg.Any<string>());
        _f.Reveal.Received().RevealClan("clan_lord1");
    }

    [TestMethod]
    public void CompletePending_TheLordIsGoneByTheEndOfTheConversation_LogsAndChangesNothing()
    {
        _f.Captive();
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");
        _f.Captives.Circle("lord1").Returns((CaptiveCircle?)null);

        _f.Service.CompletePending();

        _f.Logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("forced question was dropped")));
        _f.Actions.DidNotReceiveWithAnyArgs().ChangeRelation(default!, default);
        _f.Actions.DidNotReceive().CommitOpenPartyScreen();
    }

    [TestMethod]
    public void CompletePending_ForceOnLordsOfTwoClans_ShowsOneSummaryWithTheClanCount()
    {
        _f.Captive();
        _f.Circle.Family = new List<LordRef> { HardQuestionsFixture.Lord("wife"), HardQuestionsFixture.Lord("son", "wife") };
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.ReceivedWithAnyArgs(1).ShowRelationsHurt(default);
        _f.Actions.Received(1).ShowRelationsHurt(2);
    }

    [TestMethod]
    public void CompletePending_ForceThatChargesNobody_ShowsNoSummary()
    {
        _f.Captive();
        _f.Config.ForceLordRelationClan = 0f;
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.DidNotReceiveWithAnyArgs().ShowRelationsHurt(default);
    }

    // --- the party screen (A4) ---

    [TestMethod]
    public void CompletePending_CommitsAnOpenPartyScreenAfterAForce()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        _f.Actions.Received(1).CommitOpenPartyScreen();
    }

    [TestMethod]
    public void CompletePending_CommitsAnOpenPartyScreenAfterAStrip()
    {
        _f.Captive();
        _f.Service.QueueStrip("lord1");

        _f.Service.CompletePending();

        _f.Actions.Received(1).CommitOpenPartyScreen();
    }

    [TestMethod]
    public void CompletePending_CommitsAfterTheDeedNotBefore()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");

        _f.Service.CompletePending();

        Received.InOrder(() =>
        {
            _f.Reveal.RevealClan("clan_lord1");
            _f.Actions.CommitOpenPartyScreen();
        });
    }

    // --- strip and the queue ---

    [TestMethod]
    public void QueueStrip_MarksTheLordAndStripsHimWhenTheConversationEnds()
    {
        _f.Captive();

        _f.Service.QueueStrip("lord1");

        Assert.IsTrue(_f.State.IsStripped("lord1"));
        _f.Actions.DidNotReceiveWithAnyArgs().LoseRenown(default);

        _f.Service.CompletePending();

        _f.Actions.Received().LoseRenown(200);
    }

    [TestMethod]
    public void QueueStrip_IneligibleOrAlreadyStripped_QueuesNothing()
    {
        _f.Service.QueueStrip("nobody");
        _f.Captive();
        _f.State.MarkStripped("lord1");
        _f.Service.QueueStrip("lord1");

        _f.Service.CompletePending();

        _f.Actions.DidNotReceiveWithAnyArgs().LoseRenown(default);
    }

    [TestMethod]
    public void CompletePending_RunsOnce()
    {
        _f.Captive();
        _f.Service.QueueStrip("lord1");

        _f.Service.CompletePending();
        _f.Service.CompletePending();

        _f.Actions.Received(1).LoseRenown(200);
    }

    [TestMethod]
    public void ResetPending_DropsAQueuedDeed()
    {
        _f.Captive();
        _f.Service.QueueStrip("lord1");

        _f.Service.ResetPending();
        _f.Service.CompletePending();

        _f.Actions.DidNotReceiveWithAnyArgs().LoseRenown(default);
    }

    // --- cost of a dialog frame (B1) ---

    [TestMethod]
    public void DialogTime_NeverScansTheWorld()
    {
        _f.Captive();
        _f.Player.Gold.Returns(100000);

        _f.Service.CanAsk(HardQuestionTopic.Clan, "lord1");
        _f.Service.CanAsk(HardQuestionTopic.Kingdom, "lord1");
        _f.Service.CanStrip("lord1");
        _f.Service.Quote(HardQuestionTopic.Clan, "lord1");
        _f.Service.CanAfford(HardQuestionTopic.Clan, "lord1");
        _f.Service.ReplyKind("lord1");
        _f.Service.PayBribe(HardQuestionTopic.Clan, "lord1");
        _f.Service.QueueForce(HardQuestionTopic.Kingdom, "lord1");

        _f.Captives.DidNotReceiveWithAnyArgs().Circle(default!);
    }

    [TestMethod]
    public void CompletePending_ScansTheCircleOncePerDeed()
    {
        _f.Captive();
        _f.Random.NextDouble().Returns(0.9);
        _f.Service.QueueForce(HardQuestionTopic.Clan, "lord1");
        _f.Service.CompletePending();
        _f.Captives.Received(1).Circle("lord1");

        _f.Captive("lord2");
        _f.Service.QueueStrip("lord2");
        _f.Service.CompletePending();

        _f.Captives.Received(1).Circle("lord2");
    }

    [TestMethod]
    public void CanAsk_ATopicAlreadyUsed_DoesNotEvenProbeTheEngine()
    {
        _f.Captive();
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");

        Assert.IsFalse(_f.Service.CanAsk(HardQuestionTopic.Clan, "lord1"));

        _f.Captives.DidNotReceiveWithAnyArgs().Snapshot(default!);
    }

    [TestMethod]
    public void PayBribe_ProbesTheLordOnce()
    {
        _f.Captive();

        _f.Service.PayBribe(HardQuestionTopic.Clan, "lord1");

        _f.Captives.Received(1).Snapshot("lord1");
    }
}
