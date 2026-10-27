using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.NotableServices;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Tests.Features.NotableServices;

[TestClass]
public class NotableServicesServiceTests
{
    private static readonly NotableServicesConfig Defaults = new();

    // ---- The gain formula ----

    [TestMethod]
    public void Gain_AtRelationZero_IsMenTimesTheRate()
    {
        Assert.AreEqual(10, NotableServicesService.Gain(20, 0f, Defaults));
    }

    [TestMethod]
    public void Gain_AtRelationFifty_DividesBySix()
    {
        // 60 men x 0.5 = 30, / (1 + 50/10) = 5.
        Assert.AreEqual(5, NotableServicesService.Gain(60, 50f, Defaults));
    }

    [DataTestMethod]
    [DataRow(-9.9f)]
    [DataRow(-10f)]
    [DataRow(-10.1f)]
    [DataRow(-80f)]
    public void Gain_AtNegativeRelation_DividesByOne(float relation)
    {
        Assert.AreEqual(10, NotableServicesService.Gain(20, relation, Defaults));
    }

    [TestMethod]
    public void Gain_IsCappedAtMaxGainPerGift()
    {
        Assert.AreEqual(20, NotableServicesService.Gain(100, 0f, Defaults));
        Assert.AreEqual(7, NotableServicesService.Gain(100, 0f, new NotableServicesConfig { MaxGainPerGift = 7f }));
    }

    [TestMethod]
    public void Gain_ForNoMen_IsZero()
    {
        Assert.AreEqual(0, NotableServicesService.Gain(0, 0f, Defaults));
    }

    [TestMethod]
    public void Gain_RoundsHalfAwayFromZero()
    {
        Assert.AreEqual(1, NotableServicesService.Gain(1, 0f, Defaults));
        Assert.AreEqual(0, NotableServicesService.Gain(1, 0f, new NotableServicesConfig { GainPerMan = 0.4f }));
    }

    // ---- Where the men go ----

    [DataTestMethod]
    [DataRow(SettlementKind.Village)]
    [DataRow(SettlementKind.Town)]
    [DataRow(SettlementKind.Castle)]
    public void Done_TroopsBecomeMilitia_InEverySettlementKind(SettlementKind kind)
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(kind, new SideIds("k_free", "c_free"));
        f.Config.MilitiaPerTroop = 2f;

        f.OpenScreen()!(new GiftTally(10, 0));

        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 20f);
    }

    [TestMethod]
    public void Done_PrisonersBecomeHearth_InAVillage()
    {
        var f = new NotableServicesFixture();
        f.Config.HearthPerPrisoner = 3f;

        f.OpenScreen()!(new GiftTally(0, 4));

        f.Adapter.Received(1).AddHearth(NotableServicesFixture.SettlementId, 12f);
        f.Adapter.DidNotReceive().AddProsperity(Arg.Any<string>(), Arg.Any<float>());
        f.Adapter.DidNotReceive().AddMilitia(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Done_PrisonersBecomeProsperity_InATown()
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Town, new SideIds("k_free", "c_free"));
        f.Config.ProsperityPerPrisoner = 5f;

        f.OpenScreen()!(new GiftTally(0, 4));

        f.Adapter.Received(1).AddProsperity(NotableServicesFixture.SettlementId, 20f);
        f.Adapter.DidNotReceive().AddHearth(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Done_ACastle_TakesNoPrisonersIntoAnyScore()
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Castle, new SideIds("k_free", "c_free"));

        f.OpenScreen()!(new GiftTally(5, 4));

        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 5f);
        f.Adapter.DidNotReceive().AddHearth(Arg.Any<string>(), Arg.Any<float>());
        f.Adapter.DidNotReceive().AddProsperity(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Done_AnEmptyGift_ChangesNothing()
    {
        var f = new NotableServicesFixture();

        f.OpenScreen()!(GiftTally.Empty);

        f.Adapter.DidNotReceiveWithAnyArgs().AddMilitia(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowGiftNotice(default!, default);
        f.Grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
    }

    [TestMethod]
    public void Done_CountsAtMostMaxMenPerGift_TroopsFirst()
    {
        var f = new NotableServicesFixture();
        f.Config.MaxMenPerGift = 10f;

        f.OpenScreen()!(new GiftTally(8, 8));

        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 8f);
        f.Adapter.Received(1).AddHearth(NotableServicesFixture.SettlementId, 2f);
    }

    [TestMethod]
    public void Done_RunTwiceWithTheSameTally_PaysTheSecondCommitNothing()
    {
        var f = new NotableServicesFixture();
        var done = f.OpenScreen()!;

        done(new GiftTally(10, 0));
        done(new GiftTally(10, 0));
        done(new GiftTally(12, 0));

        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 10f);
        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 2f);
        f.Adapter.Received(2).AddMilitia(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Done_ShowsTheGiftNotice_WithTheMenAndTheSettlementId()
    {
        var f = new NotableServicesFixture();

        f.OpenScreen()!(new GiftTally(3, 2));

        f.Adapter.Received(1).ShowGiftNotice(NotableServicesFixture.SettlementId, 5);
    }

    // ---- Relation and Roguery ----

    [TestMethod]
    public void Done_GrantsTheGain_ToTheSpeaker_AndShowsIt()
    {
        var f = new NotableServicesFixture();
        f.Speaker.Relation = 0f;

        f.OpenScreen()!(new GiftTally(10, 10));

        f.Grants.Received(1).Grant(NotableServicesFixture.SpeakerId, 10, Arg.Any<string>());
        f.Adapter.Received(1).ShowRelationNotice(NotableServicesFixture.SpeakerId, 10);
    }

    [TestMethod]
    public void Done_ReadsTheRelationNow_NotWhenTheScreenOpened()
    {
        var f = new NotableServicesFixture();
        var done = f.OpenScreen()!;
        f.Speaker.Relation = 50f;

        done(new GiftTally(60, 0));

        f.Grants.Received(1).Grant(NotableServicesFixture.SpeakerId, 5, Arg.Any<string>());
    }

    [TestMethod]
    public void Done_AGainRoundingBelowOne_IsNeverSent()
    {
        var f = new NotableServicesFixture();
        f.Config.GainPerMan = 0.4f;

        f.OpenScreen()!(new GiftTally(1, 0));

        f.Grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowRelationNotice(default!, default);
        f.Adapter.Received(1).AddMilitia(NotableServicesFixture.SettlementId, 1f);
    }

    [TestMethod]
    public void Done_ShowsTheRelationThatLanded_NotTheAsk()
    {
        var f = new NotableServicesFixture();
        f.Grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1) + 3);

        f.OpenScreen()!(new GiftTally(20, 0));

        f.Grants.Received(1).Grant(NotableServicesFixture.SpeakerId, 10, Arg.Any<string>());
        f.Adapter.Received(1).ShowRelationNotice(NotableServicesFixture.SpeakerId, 13);
        f.Adapter.DidNotReceive().ShowRelationNotice(Arg.Any<string>(), 10);
    }

    [TestMethod]
    public void Done_ARelationThatDidNotLand_ShowsNoRelationNotice()
    {
        var f = new NotableServicesFixture();
        f.Grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        f.OpenScreen()!(new GiftTally(10, 0));

        f.Adapter.DidNotReceiveWithAnyArgs().ShowRelationNotice(default!, default);
    }

    [TestMethod]
    public void Done_RogueryXp_IsPrisonersTimesTheRate()
    {
        var f = new NotableServicesFixture();
        f.Config.RogueryXpPerPrisoner = 12f;

        f.OpenScreen()!(new GiftTally(7, 5));

        f.Skills.Received(1).AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", 60f);
        f.Adapter.Received(1).ShowRogueryNotice(60);
    }

    [TestMethod]
    public void Done_TheRogueryNoticeRoundsHalfAwayFromZero_LikeEveryOtherAmount()
    {
        var f = new NotableServicesFixture();
        f.Config.RogueryXpPerPrisoner = 2.5f;

        f.OpenScreen()!(new GiftTally(0, 1));

        f.Skills.Received(1).AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", 2.5f);
        f.Adapter.Received(1).ShowRogueryNotice(3);
    }

    [TestMethod]
    public void Done_TheRogueryNotice_ShowsTheExperienceThatLanded_NotTheAsk()
    {
        var f = new NotableServicesFixture();
        f.Config.RogueryXpPerPrisoner = 12f;
        f.Skills.AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", Arg.Any<float>()).Returns(87.4f);

        f.OpenScreen()!(new GiftTally(7, 5));

        f.Skills.Received(1).AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", 60f);
        f.Adapter.Received(1).ShowRogueryNotice(87);
        f.Adapter.DidNotReceive().ShowRogueryNotice(60);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.4f)]
    public void Done_TheRogueryNotice_IsSkipped_WhenNothingLanded(float landed)
    {
        var f = new NotableServicesFixture();
        f.Skills.AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", Arg.Any<float>()).Returns(landed);

        f.OpenScreen()!(new GiftTally(0, 3));

        f.Skills.Received(1).AddSkillXpLanded(NotableServicesFixture.PlayerId, "Roguery", 30f);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowRogueryNotice(default);
    }

    [TestMethod]
    public void Done_NoPrisoners_GivesNoRogueryXp()
    {
        var f = new NotableServicesFixture();

        f.OpenScreen()!(new GiftTally(7, 0));

        f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowRogueryNotice(default);
    }

    [TestMethod]
    public void Done_AnEngineFault_IsLogged_AndDoesNotEscape()
    {
        var f = new NotableServicesFixture();
        f.Adapter.When(a => a.AddMilitia(Arg.Any<string>(), Arg.Any<float>())).Do(_ => throw new System.InvalidOperationException("boom"));
        var done = f.OpenScreen()!;

        done(new GiftTally(3, 0));

        f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
    }

    // ---- The screen's transfer filter ----

    private static GiftTransfer Move(bool hero = false, bool prisoner = false, bool fromPlayer = true, string culture = "c_free", int given = 0) =>
        new(hero, prisoner, fromPlayer, NotableServicesFixture.Culture(culture), given);

    [TestMethod]
    public void Transfer_Heroes_AreNeverTransferable_EitherWay()
    {
        var f = new NotableServicesFixture();
        f.OpenScreen();

        Assert.IsFalse(f.CanTransfer!(Move(hero: true)));
        Assert.IsFalse(f.CanTransfer!(Move(hero: true, fromPlayer: false)));
        Assert.IsFalse(f.CanTransfer!(Move(hero: true, prisoner: true)));
    }

    [TestMethod]
    public void Transfer_FreeSettlement_RefusesAnEvilPrisoner()
    {
        var f = new NotableServicesFixture();
        f.OpenScreen();

        Assert.IsFalse(f.CanTransfer!(Move(prisoner: true, culture: "c_evil")));
    }

    [TestMethod]
    public void Transfer_EvilSettlement_AcceptsAFreePrisoner()
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Village, new SideIds("k_evil", "c_evil"));
        f.OpenScreen();

        Assert.IsTrue(f.CanTransfer!(Move(prisoner: true, culture: "c_free")));
    }

    [DataTestMethod]
    [DataRow("k_neutral", "c_neutral", "c_evil")]
    [DataRow("k_neutral", "c_neutral", "c_free")]
    [DataRow("k_free", "c_free", "c_neutral")]
    [DataRow("k_evil", "c_evil", "c_neutral")]
    [DataRow("k_free", "c_free", "c_free")]
    public void Transfer_NeutralOnEitherEnd_OrTheSameSide_AcceptsThePrisoner(string kingdom, string settlementCulture, string prisonerCulture)
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Village, new SideIds(kingdom, settlementCulture));
        f.OpenScreen();

        Assert.IsTrue(f.CanTransfer!(Move(prisoner: true, culture: prisonerCulture)));
    }

    // Decision 4 over every (settlement, prisoner) pair: only a Free settlement refusing an Evil prisoner is refused.
    [DataTestMethod]
    [DataRow("c_free", "c_free", true)]
    [DataRow("c_free", "c_evil", false)]
    [DataRow("c_free", "c_neutral", true)]
    [DataRow("c_evil", "c_free", true)]
    [DataRow("c_evil", "c_evil", true)]
    [DataRow("c_evil", "c_neutral", true)]
    [DataRow("c_neutral", "c_free", true)]
    [DataRow("c_neutral", "c_evil", true)]
    [DataRow("c_neutral", "c_neutral", true)]
    public void Transfer_Decision4_EveryPair(string settlementCulture, string prisonerCulture, bool accepted)
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Village, new SideIds("k_" + settlementCulture.Substring(2), settlementCulture));
        f.OpenScreen();

        Assert.AreEqual(accepted, f.CanTransfer!(Move(prisoner: true, culture: prisonerCulture)));
    }

    [TestMethod]
    public void Transfer_Troops_HaveNoAlignmentFilter()
    {
        var f = new NotableServicesFixture();
        f.OpenScreen();

        Assert.IsTrue(f.CanTransfer!(Move(culture: "c_evil")));
        Assert.IsTrue(f.CanTransfer!(Move(culture: "c_free")));
    }

    [TestMethod]
    public void Transfer_TakingAnOpposedPrisonerBack_IsAlwaysAllowed()
    {
        var f = new NotableServicesFixture();
        f.OpenScreen();

        Assert.IsTrue(f.CanTransfer!(Move(prisoner: true, fromPlayer: false, culture: "c_evil")));
    }

    [TestMethod]
    public void Transfer_ACastle_TakesNoPrisoners_ButTakesTroops()
    {
        var f = new NotableServicesFixture();
        f.SettlementIs(SettlementKind.Castle, new SideIds("k_free", "c_free"));
        f.OpenScreen();

        Assert.IsFalse(f.CanTransfer!(Move(prisoner: true)));
        Assert.IsTrue(f.CanTransfer!(Move()));
    }

    [TestMethod]
    public void Transfer_AboveMaxMenPerGift_IsRefused_ButTakingBackIsNot()
    {
        var f = new NotableServicesFixture();
        f.Config.MaxMenPerGift = 10f;
        f.OpenScreen();

        Assert.IsTrue(f.CanTransfer!(Move(given: 9)));
        Assert.IsFalse(f.CanTransfer!(Move(given: 10)));
        Assert.IsFalse(f.CanTransfer!(Move(prisoner: true, given: 12)));
        Assert.IsTrue(f.CanTransfer!(Move(fromPlayer: false, given: 12)));
    }

    [TestMethod]
    public void Finish_IsPossibleUpToTheCap_AndNotAbove()
    {
        var f = new NotableServicesFixture();
        f.Config.MaxMenPerGift = 10f;
        f.OpenScreen();

        Assert.IsTrue(f.CanFinish!(new GiftTally(6, 4)));
        Assert.IsFalse(f.CanFinish!(new GiftTally(6, 5)));
    }

    [TestMethod]
    public void Gift_OpensTheScreen_ForTheSpeakersSettlement_OnlyAfterTheConversation()
    {
        var f = new NotableServicesFixture();

        f.Service.QueueGift();

        Assert.IsNotNull(f.Queued);
        Assert.IsNull(f.ScreenSettlement, "the screen must wait for the conversation to end");
        f.Queued!();
        Assert.AreEqual(NotableServicesFixture.SettlementId, f.ScreenSettlement);
    }

    [TestMethod]
    public void Gift_TheScreenShowsTheCapAsItsRoom()
    {
        var f = new NotableServicesFixture();
        f.Config.MaxMenPerGift = 40f;

        f.OpenScreen();

        Assert.AreEqual(40, f.MenLimit);
    }

    [TestMethod]
    public void Gift_IsHidden_AndOpensNothing_WhileAPartyScreenIsOpen()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PartyScreenIsOpen.Returns(true);

        Assert.IsFalse(f.Service.OffersGift());
        f.Service.QueueGift();

        Assert.IsNull(f.Queued);
        Assert.IsNull(f.ScreenSettlement);
        Assert.IsNotNull(f.Service.QuoteIntroduce(), "the other lines do not write the live rosters");
        Assert.IsTrue(f.Service.OffersReveal());
    }

    [TestMethod]
    public void Gift_AScreenOpeningBeforeTheConversationEnds_OpensNothing()
    {
        var f = new NotableServicesFixture();
        f.Service.QueueGift();
        f.Adapter.PartyScreenIsOpen.Returns(true);

        f.Queued!();

        Assert.IsNull(f.ScreenSettlement);
    }

    [TestMethod]
    public void Gift_IsHidden_AndOpensNothing_WhenThePlayerHasNoMenToGive()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PlayerHasMenToGive().Returns(false);

        Assert.IsFalse(f.Service.OffersGift());
        f.Service.QueueGift();

        Assert.IsNull(f.Queued);
        Assert.IsNotNull(f.Service.QuoteIntroduce());
        Assert.IsTrue(f.Service.OffersReveal());
    }

    [TestMethod]
    public void Gift_TheMenCheckIsRepeatedWhenTheConversationEnds()
    {
        var f = new NotableServicesFixture();
        f.Service.QueueGift();
        f.Adapter.PlayerHasMenToGive().Returns(false);

        f.Queued!();

        Assert.IsNull(f.ScreenSettlement);
    }

    [TestMethod]
    public void Gift_AtWar_QueuesNothing_AndOpensNothing()
    {
        var f = new NotableServicesFixture();
        f.Speaker.AtWarWithPlayer = true;

        f.Service.QueueGift();

        Assert.IsNull(f.Queued);
        Assert.IsNull(f.ScreenSettlement);
    }

    [TestMethod]
    public void Gift_AGateClosingBeforeTheConversationEnds_OpensNothing()
    {
        var f = new NotableServicesFixture();
        f.Service.QueueGift();
        f.Settings.Enabled.Returns(false);

        f.Queued!();

        Assert.IsNull(f.ScreenSettlement);
    }

    // ---- Who offers, and when ----

    private static IEnumerable<object[]> HidingStates()
    {
        yield return new object[] { "preacher", (System.Action<NotableServicesFixture>)(f => f.Speaker.IsPreacher = true) };
        yield return new object[] { "not a notable", (System.Action<NotableServicesFixture>)(f => f.Speaker.IsNotable = false) };
        yield return new object[] { "notable elsewhere", (System.Action<NotableServicesFixture>)(f => f.Speaker.InPlayersSettlement = false) };
        yield return new object[] { "messenger open", (System.Action<NotableServicesFixture>)(f => f.Messenger.IsOpen.Returns(true)) };
        yield return new object[] { "co-op session", (System.Action<NotableServicesFixture>)(f => f.Coop.IsSessionActive.Returns(true)) };
        yield return new object[] { "defer to host", (System.Action<NotableServicesFixture>)(f => f.Coop.ShouldDeferToHost.Returns(true)) };
        yield return new object[] { "co-op client", (System.Action<NotableServicesFixture>)(f => f.Coop.IsAuthority.Returns(false)) };
        yield return new object[] { "dedicated server", (System.Action<NotableServicesFixture>)(f => f.Server.IsDedicatedServer.Returns(true)) };
        yield return new object[] { "toggle off", (System.Action<NotableServicesFixture>)(f => f.Settings.Enabled.Returns(false)) };
        yield return new object[] { "no snapshot", (System.Action<NotableServicesFixture>)(f => f.Adapter.Snapshot(NotableServicesFixture.SpeakerId).Returns((NotableSnapshot?)null)) };
        yield return new object[] { "no partner", (System.Action<NotableServicesFixture>)(f => f.Adapter.ConversationPartnerId.Returns((string?)null)) };
    }

    [DataTestMethod]
    [DynamicData(nameof(HidingStates), DynamicDataSourceType.Method)]
    public void AllThreeLines_AreHidden_When(string state, System.Action<NotableServicesFixture> arrange)
    {
        var f = new NotableServicesFixture();
        arrange(f);

        Assert.IsFalse(f.Service.OffersGift(), state + ": gift");
        Assert.IsNull(f.Service.QuoteIntroduce(), state + ": introduce");
        Assert.IsFalse(f.Service.OffersReveal(), state + ": reveal");
    }

    [TestMethod]
    public void AllThreeLines_AreShown_ForAHeadmanInThePlayersSettlement_AtPeace()
    {
        var f = new NotableServicesFixture();

        Assert.IsTrue(f.Service.OffersGift());
        Assert.IsNotNull(f.Service.QuoteIntroduce());
        Assert.IsTrue(f.Service.OffersReveal());
    }

    [TestMethod]
    public void AtWar_OnlyTheGiftLineIsHidden()
    {
        var f = new NotableServicesFixture();
        f.Speaker.AtWarWithPlayer = true;

        Assert.IsFalse(f.Service.OffersGift());
        Assert.IsNotNull(f.Service.QuoteIntroduce());
        Assert.IsTrue(f.Service.OffersReveal());
    }

    [TestMethod]
    public void TheConditions_MakeNoWrite()
    {
        var f = new NotableServicesFixture();

        f.Service.OffersGift();
        f.Service.QuoteIntroduce();
        f.Service.OffersReveal();
        f.Service.CanAffordReveal();

        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().MarkMet(default!);
        f.Adapter.DidNotReceiveWithAnyArgs().RunAfterConversation(default!);
        f.Reveal.DidNotReceiveWithAnyArgs().Track(default!);
        f.Reveal.DidNotReceiveWithAnyArgs().FortificationsAround(default!);
    }

    // ---- Introduce ----

    [TestMethod]
    public void Introduce_PriceIsTheCountTimesThePrice()
    {
        var f = new NotableServicesFixture();

        var quote = f.Service.QuoteIntroduce()!;

        Assert.AreEqual(2, quote.Count);
        Assert.AreEqual(100, quote.Price);
        Assert.IsTrue(quote.CanAfford);
    }

    [TestMethod]
    public void Introduce_FollowsTheConfiguredPrice()
    {
        var f = new NotableServicesFixture();
        f.Config.IntroducePrice = 75f;

        Assert.AreEqual(150, f.Service.QuoteIntroduce()!.Price);
    }

    [TestMethod]
    public void Introduce_IsHiddenWhenNobodyIsUnmet()
    {
        var f = new NotableServicesFixture();
        f.Adapter.UnmetNotables(NotableServicesFixture.SettlementId).Returns(new List<string>());

        Assert.IsNull(f.Service.QuoteIntroduce());
    }

    [TestMethod]
    public void Introduce_TheAdapterListIncludesTheSpeaker_ButTheCountThePriceAndMarkMetLeaveHimOut()
    {
        var f = new NotableServicesFixture();
        f.Adapter.UnmetNotables(NotableServicesFixture.SettlementId)
            .Returns(new List<string> { "notable2", NotableServicesFixture.SpeakerId, "notable3" });

        var quote = f.Service.QuoteIntroduce()!;
        Assert.IsTrue(f.Service.Introduce());

        Assert.AreEqual(2, quote.Count);
        Assert.AreEqual(100, quote.Price);
        f.Adapter.Received(1).PayNotable(NotableServicesFixture.SpeakerId, 100);
        f.Adapter.Received(1).MarkMet(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "notable2", "notable3" })));
        f.Adapter.Received(1).ShowIntroducedNotice(2);
    }

    [TestMethod]
    public void Introduce_WhenTheSpeakerIsTheOnlyUnmetNotable_TheLineIsHidden()
    {
        var f = new NotableServicesFixture();
        f.Adapter.UnmetNotables(NotableServicesFixture.SettlementId).Returns(new List<string> { NotableServicesFixture.SpeakerId });

        Assert.IsNull(f.Service.QuoteIntroduce());
        Assert.IsFalse(f.Service.Introduce());
        f.Adapter.DidNotReceiveWithAnyArgs().MarkMet(default!);
    }

    [TestMethod]
    public void Introduce_IsGreyedWhenThePlayerIsShort()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PlayerGold.Returns(99);

        var quote = f.Service.QuoteIntroduce()!;

        Assert.IsFalse(quote.CanAfford);
    }

    [TestMethod]
    public void Introduce_PaysTheSpeakerThePrice_ThenMarksEveryoneMet()
    {
        var f = new NotableServicesFixture();

        Assert.IsTrue(f.Service.Introduce());

        Received.InOrder(() =>
        {
            f.Adapter.PayNotable(NotableServicesFixture.SpeakerId, 100);
            f.Adapter.MarkMet(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "notable2", "notable3" })));
        });
        f.Adapter.Received(1).ShowIntroducedNotice(2);
    }

    [TestMethod]
    public void Introduce_AFailedCharge_MarksNobody()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PayNotable(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        Assert.IsFalse(f.Service.Introduce());

        f.Adapter.DidNotReceiveWithAnyArgs().MarkMet(default!);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowIntroducedNotice(default);
    }

    [TestMethod]
    public void Introduce_AShortPurse_ChargesNothingAndMarksNobody()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PlayerGold.Returns(10);

        Assert.IsFalse(f.Service.Introduce());

        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().MarkMet(default!);
    }

    [TestMethod]
    public void Introduce_WithAClosedGate_DoesNothing()
    {
        var f = new NotableServicesFixture();
        f.Messenger.IsOpen.Returns(true);

        Assert.IsFalse(f.Service.Introduce());

        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
    }

    [TestMethod]
    public void Introduce_AFreeIntroduction_MarksWithoutACharge()
    {
        var f = new NotableServicesFixture();
        f.Config.IntroducePrice = 0f;

        Assert.IsTrue(f.Service.Introduce());

        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
        f.Adapter.Received(1).MarkMet(Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Introduce_AnEngineFault_IsLogged_AndMarksNobody()
    {
        var f = new NotableServicesFixture();
        f.Adapter.PayNotable(Arg.Any<string>(), Arg.Any<int>()).Returns(_ => throw new System.InvalidOperationException("boom"));

        Assert.IsFalse(f.Service.Introduce());

        f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
        f.Adapter.DidNotReceiveWithAnyArgs().MarkMet(default!);
    }

    // ---- Reveal ----

    private static NotableServicesFixture RevealFixture(params RevealCandidate[] around)
    {
        var f = new NotableServicesFixture();
        f.Reveal.FortificationsAround(NotableServicesFixture.SettlementId).Returns(around);
        f.Reveal.Track(Arg.Any<IReadOnlyList<string>>()).Returns(call => call.Arg<IReadOnlyList<string>>().Count);
        return f;
    }

    private static void AssertNoChargeNoTrack(NotableServicesFixture f)
    {
        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
        f.Reveal.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void Reveal_AtWar_IsRefused_WithNoChargeAndNoTrack()
    {
        var f = RevealFixture(new RevealCandidate("town_a", 10f, false));
        f.Speaker.AtWarWithPlayer = true;

        Assert.AreEqual(RevealOutcome.AtWar, f.Service.Reveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_WhenCold_IsRefused_WithNoChargeAndNoTrack()
    {
        var f = RevealFixture(new RevealCandidate("town_a", 10f, false));
        f.Config.RevealMinRelation = 5f;
        f.Speaker.Relation = 4f;

        Assert.AreEqual(RevealOutcome.Cold, f.Service.Reveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_AtExactlyTheMinimumRelation_IsAllowed()
    {
        var f = RevealFixture(new RevealCandidate("town_a", 10f, false));
        f.Config.RevealMinRelation = 5f;
        f.Speaker.Relation = 5f;

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());
    }

    [TestMethod]
    public void Reveal_NothingNew_ChargesNothing()
    {
        var f = RevealFixture(new RevealCandidate("town_a", 10f, true), new RevealCandidate("town_b", 900f, false));

        Assert.AreEqual(RevealOutcome.NothingNew, f.Service.Reveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_NoCandidatesAtAll_IsNothingNew()
    {
        var f = RevealFixture();

        Assert.AreEqual(RevealOutcome.NothingNew, f.Service.Reveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_PicksTheNearestUntrackedWithinTheBound()
    {
        // Bound: 0.2 x 1000 = 200. Count: 3.
        var f = RevealFixture(
            new RevealCandidate("far", 250f, false),
            new RevealCandidate("tracked", 5f, true),
            new RevealCandidate("c", 150f, false),
            new RevealCandidate("a", 40f, false),
            new RevealCandidate("edge", 200f, false),
            new RevealCandidate("b", 90f, false));

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());

        f.Reveal.Received(1).Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "a", "b", "c" })));
    }

    [TestMethod]
    public void Reveal_TheCountAndTheBound_FollowTheConfig()
    {
        var f = RevealFixture(
            new RevealCandidate("a", 40f, false), new RevealCandidate("b", 90f, false), new RevealCandidate("c", 150f, false));
        f.Config.RevealCount = 1.6f;
        f.Config.RevealMaxDiagonalFraction = 0.1f;

        f.Service.Reveal();

        f.Reveal.Received(1).Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "a", "b" })));
    }

    [TestMethod]
    public void Reveal_ChargesTheSpeakerOnce_AndShowsTheCount()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false), new RevealCandidate("b", 90f, false));

        f.Service.Reveal();

        f.Adapter.Received(1).PayNotable(NotableServicesFixture.SpeakerId, 100);
        f.Reveal.Received(1).ShowHoldsMarkedNotice(2);
    }

    [TestMethod]
    public void Reveal_ChargesBeforeItTracks()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));

        f.Service.Reveal();

        Received.InOrder(() =>
        {
            f.Adapter.PayNotable(NotableServicesFixture.SpeakerId, 100);
            f.Reveal.Track(Arg.Any<IReadOnlyList<string>>());
        });
    }

    [TestMethod]
    public void Reveal_AFailedCharge_TracksNothing()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));
        f.Adapter.PayNotable(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());

        f.Reveal.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void Reveal_AShortPurse_IsUnavailable_WithNoChargeAndNoTrack()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));
        f.Adapter.PlayerGold.Returns(99);

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());
        Assert.IsFalse(f.Service.CanAffordReveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_WithAClosedGate_IsUnavailable()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));
        f.Settings.Enabled.Returns(false);

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());

        AssertNoChargeNoTrack(f);
    }

    [TestMethod]
    public void Reveal_AFreeRequest_TracksWithoutACharge()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));
        f.Config.RevealPrice = 0f;

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());

        f.Adapter.DidNotReceiveWithAnyArgs().PayNotable(default!, default);
        f.Reveal.Received(1).Track(Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Reveal_AnEngineFault_IsLogged_AndAnswersUnavailable()
    {
        var f = RevealFixture(new RevealCandidate("a", 40f, false));
        f.Reveal.Track(Arg.Any<IReadOnlyList<string>>()).Returns(_ => throw new System.InvalidOperationException("boom"));

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());

        f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
    }

    [TestMethod]
    public void RevealPrice_IsTheRoundedConfigValue()
    {
        var f = new NotableServicesFixture();
        f.Config.RevealPrice = 99.6f;

        Assert.AreEqual(100, f.Service.RevealPrice);
    }
}
