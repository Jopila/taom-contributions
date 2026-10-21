using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The state of one gift conversation: the gates the dialog lines call, the list hand-off, the pick, the ledger it owns and
/// its three catch blocks. No engine type is involved (the conversation adapter is a fake), so these run without the game.
/// </summary>
[TestClass]
public class GiftConversationFlowTests
{
    private GiftFixture _f = null!;
    private IGiftConversationAdapter _conversation = null!;
    private ICoopSessionProvider _coop = null!;
    private GiftConversationFlow _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new GiftFixture();
        _conversation = Substitute.For<IGiftConversationAdapter>();
        _conversation.PartnerId.Returns(GiftFixture.HeroId);
        _coop = Substitute.For<ICoopSessionProvider>();
        _sut = new GiftConversationFlow(_f.Sut, _conversation, _coop, _f.Logger);
    }

    // The option is built before the call that configures the substitute: NSubstitute cannot take another substitute call inside Returns.
    private void Select(GiftOption option) => _conversation.SelectedOption.Returns(option);

    private void SelectTheOnlyOption(GiftEntry entry) => Select(_f.Sut.Options(GiftFixture.HeroId, entry).Single());

    private void AFaultInTheFacts() => _f.Heroes.ReadPlayer().Returns(_ => throw new InvalidOperationException("boom"));

    // ---- CanOffer

    [TestMethod]
    public void CanOffer_AnOpenLord_IsTrue()
    {
        Assert.IsTrue(_sut.CanOffer(GiftEntry.Lord));
    }

    [TestMethod]
    public void CanOffer_UnderCoopDeferral_IsFalse_AndReadsNoFacts()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.CanOffer(GiftEntry.Lord));
        _f.Heroes.DidNotReceiveWithAnyArgs().ReadRecipient(default!);
    }

    [TestMethod]
    public void CanOffer_NoConversationPartner_IsFalse()
    {
        _conversation.PartnerId.Returns((string?)null);

        Assert.IsFalse(_sut.CanOffer(GiftEntry.Lord));
        _f.Heroes.DidNotReceiveWithAnyArgs().ReadRecipient(default!);
    }

    [TestMethod]
    public void CanOffer_LordAtWar_IsFalse()
    {
        _f.Recipient = GiftFixture.Lord() with { IsAtWarWithPlayer = true };

        Assert.IsFalse(_sut.CanOffer(GiftEntry.Lord));
    }

    [TestMethod]
    public void CanOffer_AHostileLord_StillShowsTheLine()
    {
        _f.Recipient = GiftFixture.Lord(relation: -80);

        Assert.IsTrue(_sut.CanOffer(GiftEntry.Lord));
    }

    [TestMethod]
    public void CanOffer_InsideTheCooldown_ShowsTheLineGreyed()
    {
        _sut.Ledger.Stamp(GiftFixture.HeroId, 98);

        Assert.IsTrue(_sut.CanOffer(GiftEntry.Lord));
    }

    [TestMethod]
    public void CanOffer_AFailingRead_IsFalse_AndLogsTheError()
    {
        AFaultInTheFacts();

        Assert.IsFalse(_sut.CanOffer(GiftEntry.Lord));
        _f.Logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    // ---- CanChoose

    [TestMethod]
    public void CanChoose_OpenGate_IsTrue_WithNoDaysLeft()
    {
        Assert.IsTrue(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
    }

    [TestMethod]
    public void CanChoose_HiddenGate_IsFalse()
    {
        _f.Recipient = GiftFixture.Lord() with { IsAtWarWithPlayer = true };

        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
    }

    [TestMethod]
    public void CanChoose_HostileRefusal_IsStillAChoice()
    {
        _f.Recipient = GiftFixture.Lord(relation: -75);

        Assert.IsTrue(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
    }

    [TestMethod]
    public void CanChoose_AlignmentRefusal_IsStillAChoice()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };

        Assert.IsTrue(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
    }

    [TestMethod]
    public void CanChoose_InsideTheCooldown_IsFalse_AndSaysHowManyDaysAreLeft()
    {
        _sut.Ledger.Stamp(GiftFixture.HeroId, 98);

        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(5, daysLeft);
    }

    [TestMethod]
    public void CanChoose_NoConversationPartner_IsFalse_AndReadsNothing()
    {
        _conversation.PartnerId.Returns((string?)null);

        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
        _f.Heroes.DidNotReceiveWithAnyArgs().ReadRecipient(default!);
    }

    [TestMethod]
    public void CanChoose_UnderCoopDeferral_IsFalse()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out _));
        _f.Heroes.DidNotReceiveWithAnyArgs().ReadRecipient(default!);
    }

    [TestMethod]
    public void CanChoose_AFailingRead_IsFalse_AndLogsTheError()
    {
        AFaultInTheFacts();

        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out var daysLeft));
        Assert.AreEqual(0, daysLeft);
        _f.Logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    // ---- Ask

    [TestMethod]
    public void Ask_OpenGate_HandsTheListToTheConversation()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Open));
        Assert.IsFalse(_sut.HasNoOptions());
        _conversation.Received(1).SetOptions(Arg.Is<IReadOnlyList<GiftOption>>(o => o.Count == 1 && o[0].ItemId == "ring"));
        _conversation.ReceivedWithAnyArgs(1).SetOptions(default!);
    }

    [TestMethod]
    public void Ask_OpenGateWithNothingToGive_ReportsNoOptions_AndHandsOverAnEmptyList()
    {
        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Open));
        Assert.IsTrue(_sut.HasNoOptions());
        _conversation.Received(1).SetOptions(Arg.Is<IReadOnlyList<GiftOption>>(o => o.Count == 0));
    }

    [TestMethod]
    public void Ask_HostileLord_RefusesAndCostsOnePoint_AndListsNothing()
    {
        _f.Recipient = GiftFixture.Lord(relation: -75);

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.RefusesHostile));
        Assert.IsFalse(_sut.HasNoOptions(), "a refusal is not an empty bag");
        _f.Relations.Received(1).Grant(GiftFixture.HeroId, -1, Arg.Any<string>());
        _conversation.DidNotReceiveWithAnyArgs().SetOptions(default!);
    }

    [TestMethod]
    public void Ask_AlignmentRefusal_CostsNothing_AndListsNothing()
    {
        _f.Player = _f.Player! with { SideFactionId = "mordor", SideCultureId = "mordor" };

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.RefusesAlignment));
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _conversation.DidNotReceiveWithAnyArgs().SetOptions(default!);
    }

    [TestMethod]
    public void Ask_InsideTheCooldown_IsTheCooldownGate()
    {
        _sut.Ledger.Stamp(GiftFixture.HeroId, 99);

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Cooldown));
        _conversation.DidNotReceiveWithAnyArgs().SetOptions(default!);
    }

    [TestMethod]
    public void Ask_UnderCoopDeferral_DoesNothingAtAll()
    {
        _f.Recipient = GiftFixture.Lord(relation: -75);
        _coop.ShouldDeferToHost.Returns(true);

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Hidden));
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_NoPartner_DoesNothing()
    {
        _conversation.PartnerId.Returns((string?)null);

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Hidden));
        _f.Relations.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void Ask_AFaultInTheService_ClosesTheGate_AndLogsTheError()
    {
        AFaultInTheFacts();

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Hidden));
        Assert.IsFalse(_sut.HasNoOptions());
        _f.Logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Ask_AFaultHandingOverTheList_ClosesTheGate()
    {
        _conversation.When(c => c.SetOptions(Arg.Any<IReadOnlyList<GiftOption>>())).Do(_ => throw new InvalidOperationException("boom"));

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Hidden));
        _f.Logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Ask_AfterAGivenGift_ClearsTheLastOutcome()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 2));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _sut.Pick();
        Assert.IsTrue(_sut.Gave());

        _sut.Ask(GiftEntry.Lord);

        Assert.IsFalse(_sut.Gave(), "a new ask clears the last outcome");
        Assert.IsFalse(_sut.GaveSuited());
    }

    // ---- the row

    [TestMethod]
    public void ListRow_WritesTheRowOfTheCurrentOption()
    {
        var option = new GiftOption(GiftOptionKind.Good, "ring", "Ring", 1, 0, 8, 0, 8, GiftBlock.None);
        _conversation.CurrentOption.Returns(option);

        Assert.IsTrue(_sut.ListRow());
        _conversation.Received(1).WriteRow(option);
    }

    [TestMethod]
    public void ListRow_NoCurrentOption_IsFalse_AndWritesNothing()
    {
        _conversation.CurrentOption.Returns((GiftOption?)null);

        Assert.IsFalse(_sut.ListRow());
        _conversation.DidNotReceiveWithAnyArgs().WriteRow(default!);
    }

    [DataTestMethod]
    [DataRow(GiftBlock.None)]
    [DataRow(GiftBlock.CannotAfford)]
    [DataRow(GiftBlock.NoEffect)]
    public void RowBlock_IsTheCurrentOptionsBlock(GiftBlock block)
    {
        _conversation.CurrentOption.Returns(new GiftOption(GiftOptionKind.Purse, "", "", 0, 500, 1, 0, 1, block));

        Assert.AreEqual(block, _sut.RowBlock());
    }

    [TestMethod]
    public void RowBlock_NoCurrentOption_IsNull()
    {
        _conversation.CurrentOption.Returns((GiftOption?)null);

        Assert.IsNull(_sut.RowBlock());
    }

    // ---- Pick

    [TestMethod]
    public void Pick_AGift_IsGiven_AndStartsTheCooldown()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 2));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);

        _sut.Pick();

        Assert.IsTrue(_sut.Gave());
        Assert.IsFalse(_sut.GaveSuited(), "not to the hero's taste");
        Assert.AreEqual(100, _sut.Ledger.LastGiftDay(GiftFixture.HeroId));
        Assert.IsFalse(_sut.CanChoose(GiftEntry.Lord, out var daysLeft), "a second gift is refused until the cooldown ends");
        Assert.AreEqual(7, daysLeft);
    }

    [TestMethod]
    public void Pick_AGiftTheHeroPrizes_IsSuited()
    {
        _f.Recipient = GiftFixture.Lord(race: "dwarf");
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);

        _sut.Pick();

        Assert.IsTrue(_sut.GaveSuited());
        Assert.IsTrue(_sut.Gave());
    }

    [TestMethod]
    public void Pick_ASecondGiftInsideTheCooldown_IsRefusedAtTheAsk()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400, 3));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _sut.Pick();

        _sut.Ask(GiftEntry.Lord);

        Assert.IsTrue(_sut.GateIs(GiftGateKind.Cooldown));
        _f.Roster.Received(1).RemoveItem("ring", 1);
    }

    [TestMethod]
    public void Pick_ItemGoneBeforeTheChoice_FailsAndStartsNoCooldown()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _f.Held.Clear();

        _sut.Pick();

        Assert.IsFalse(_sut.Gave());
        Assert.IsNull(_sut.Ledger.LastGiftDay(GiftFixture.HeroId));
    }

    [TestMethod]
    public void Pick_NothingSelected_FailsQuietly()
    {
        _sut.Ask(GiftEntry.Lord);
        _conversation.SelectedOption.Returns((GiftOption?)null);

        _sut.Pick();

        Assert.IsFalse(_sut.Gave());
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Pick_NoPartner_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _conversation.PartnerId.Returns((string?)null);

        _sut.Pick();

        Assert.IsFalse(_sut.Gave());
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
    }

    [TestMethod]
    public void Pick_UnderCoopDeferral_TakesNothing()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _coop.ShouldDeferToHost.Returns(true);

        _sut.Pick();

        Assert.IsFalse(_sut.Gave());
        _f.Roster.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        Assert.IsNull(_sut.Ledger.LastGiftDay(GiftFixture.HeroId));
    }

    [TestMethod]
    public void Pick_AFaultWhileGiving_FailsAndLogsTheError()
    {
        _f.Held.Add(GiftFixture.Good("ring", "jewelry", 400));
        _sut.Ask(GiftEntry.Lord);
        SelectTheOnlyOption(GiftEntry.Lord);
        _f.Roster.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Pick();

        Assert.IsFalse(_sut.Gave());
        Assert.IsNull(_sut.Ledger.LastGiftDay(GiftFixture.HeroId));
        _f.Logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Pick_APurseToACompanion_MovesTheGold_AndStartsTheCooldown()
    {
        _f.Recipient = GiftFixture.Companion();
        _sut.Ask(GiftEntry.Companion);
        Select(_f.Sut.Options(GiftFixture.HeroId, GiftEntry.Companion).First(o => o.Amount == 500));

        _sut.Pick();

        Assert.IsTrue(_sut.Gave());
        _f.Heroes.Received(1).PayGoldToHero(GiftFixture.HeroId, 500);
        Assert.AreEqual(100, _sut.Ledger.LastGiftDay(GiftFixture.HeroId));
    }

    // ---- the ledger

    [TestMethod]
    public void EveryFlow_OwnsItsOwnLedger()
    {
        var other = new GiftConversationFlow(_f.Sut, _conversation, _coop, _f.Logger);
        _sut.Ledger.Stamp(GiftFixture.HeroId, 99);

        Assert.AreNotSame(_sut.Ledger, other.Ledger);
        Assert.IsNull(other.Ledger.LastGiftDay(GiftFixture.HeroId));
    }
}
