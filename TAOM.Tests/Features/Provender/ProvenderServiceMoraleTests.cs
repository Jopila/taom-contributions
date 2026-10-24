using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;
using static TAOM.Tests.Features.Provender.ProvenderFixture;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The morale options: pay every soldier (small or large tier) or hand out the ration the host prefers. Pay never disorganizes,
/// drink always does. Every closed state has its own test, and a failed write gives no morale.
/// </summary>
[TestClass]
public class ProvenderServiceMoraleTests
{
    private ProvenderFixture _f = null!;
    private ProvenderService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ProvenderFixture();
        _sut = _f.Service();
    }

    private void AssertNoMorale() => _f.World.DidNotReceiveWithAnyArgs().AddMorale(default, default);

    // ---- pay

    [TestMethod]
    public void Quote_SmallPay_IsTheSettingsGoldPerSoldier()
    {
        var q = _sut.Quote(MoraleOption.PaySmall);

        Assert.AreEqual(ProvenderBlock.None, q.Block);
        Assert.AreEqual(50, q.PerTroopGold);
        Assert.AreEqual(50 * 40, q.Gold);
        Assert.AreEqual(5, q.Morale);
    }

    [TestMethod]
    public void Buy_LargePay_TakesTheGoldAndAddsMoraleWithoutDisorder()
    {
        Assert.IsTrue(_sut.Buy(MoraleOption.PayLarge));

        _f.World.Received(1).PayTroops(100 * 40);
        _f.World.Received(1).AddMorale(10, false);
        _f.World.Received(1).CommitPartyScreen();
    }

    [TestMethod]
    public void Quote_PayMoreThanTheGold_IsClosedForGold()
    {
        _f.Party(new PartyBuilder { Gold = 1999 });

        Assert.AreEqual(ProvenderBlock.NoGold, _sut.Quote(MoraleOption.PaySmall).Block);
        Assert.IsFalse(_sut.Buy(MoraleOption.PaySmall));
        _f.World.DidNotReceiveWithAnyArgs().PayTroops(default);
        AssertNoMorale();
    }

    [TestMethod]
    public void Quote_APayThatOverflowsAnInt_IsClosedForGold()
    {
        _f.Settings = _f.Settings with { LargePayGold = 500 };
        _f.Party(new PartyBuilder { Troops = int.MaxValue / 100, Gold = int.MaxValue });

        Assert.AreEqual(ProvenderBlock.NoGold, _sut.Quote(MoraleOption.PayLarge).Block);
    }

    [TestMethod]
    public void Buy_ThePayIsTakenShort_GivesNoMorale()
    {
        _f.World.PayTroops(Arg.Any<int>()).Returns(10);

        Assert.IsFalse(_sut.Buy(MoraleOption.PaySmall));

        AssertNoMorale();
    }

    // ---- where and who

    [TestMethod]
    public void Quote_OnTheOpenMap_IsClosed()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = false });

        Assert.AreEqual(ProvenderBlock.NotInPlace, _sut.Quote(MoraleOption.PaySmall).Block);
    }

    [TestMethod]
    public void Quote_OnTheOpenMap_StillCarriesThePayFigures_ForTheGreyedLine()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = false });

        var q = _sut.Quote(MoraleOption.PayLarge);

        Assert.AreEqual(ProvenderBlock.NotInPlace, q.Block);
        Assert.AreEqual(100, q.PerTroopGold);
        Assert.AreEqual(100 * 40, q.Gold);
        Assert.AreEqual(10, q.Morale);
    }

    [TestMethod]
    public void Quote_Drink_OnTheOpenMap_StillNamesTheRationAndCount_ForTheGreyedLine()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = false });
        _f.Items.GetItemCount("beer").Returns(10);

        var q = _sut.Quote(MoraleOption.Drink);

        Assert.AreEqual(ProvenderBlock.NotInPlace, q.Block);
        Assert.AreEqual("beer", q.ItemId);
        Assert.AreEqual(6, q.ItemCount);
        Assert.IsTrue(q.HasItem);
        Assert.IsFalse(q.QuotesPurchase);
    }

    [TestMethod]
    public void Quote_NoTroops_OutranksNotInPlace()
    {
        _f.Party(new PartyBuilder { Troops = 0, SettlementId = null, CampReady = false });

        Assert.AreEqual(ProvenderBlock.NoTroops, _sut.Quote(MoraleOption.PaySmall).Block);
    }

    [TestMethod]
    public void Quote_Drink_APricedShortfall_QuotesThePurchase_AnUnpricedOneDoesNot()
    {
        _f.Items.GetItemCount("beer").Returns(2);
        _f.Offer("beer", 25, 10);
        var priced = _sut.Quote(MoraleOption.Drink);
        Assert.IsTrue(priced.QuotesPurchase);
        Assert.AreEqual(4, priced.ToBuy);
        Assert.AreEqual(100, priced.Gold);

        _f.Party(new PartyBuilder { SettlementId = null, CampReady = true });
        var camp = _sut.Quote(MoraleOption.Drink);
        Assert.AreEqual(ProvenderBlock.NoMarket, camp.Block);
        Assert.IsFalse(camp.QuotesPurchase, "no market: the cost is unknown, so the line must not quote one");
    }

    [TestMethod]
    public void Quote_OnADedicatedServer_IsClosed()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.AreEqual(ProvenderBlock.Coop, _sut.Quote(MoraleOption.PaySmall).Block);
        Assert.IsFalse(_sut.Buy(MoraleOption.PaySmall));
        AssertNoMorale();
    }

    [DataTestMethod]
    [DataRow(0, 0, true, false)]
    [DataRow(4, 100, false, true)]
    [DataRow(4, 0, true, false)]
    public void MoraleQuote_ExactlyOneDrinkLineLists_WhenARationIsChosen(int toBuy, int gold, bool fromStores, bool purchase)
    {
        var q = new MoraleQuote(ProvenderBlock.None, gold, 15, 0, "beer", 6, toBuy);

        Assert.AreEqual(fromStores, q.ListsFromStores);
        Assert.AreEqual(purchase, q.QuotesPurchase);
        Assert.AreNotEqual(q.ListsFromStores, q.QuotesPurchase, "exactly one drink line lists");
    }

    [TestMethod]
    public void MoraleQuote_NoRation_ListsNeitherDrinkLine()
    {
        var q = MoraleQuote.Closed(ProvenderBlock.NoRation);

        Assert.IsFalse(q.ListsFromStores);
        Assert.IsFalse(q.QuotesPurchase);
    }

    [TestMethod]
    public void Quote_AClosedGate_HasNoItem()
    {
        _f.Settings = _f.Settings with { Enabled = false };

        Assert.IsFalse(_sut.Quote(MoraleOption.Drink).HasItem);
    }

    [TestMethod]
    public void Quote_InAReadyFieldCamp_IsOpen()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = true });

        Assert.AreEqual(ProvenderBlock.None, _sut.Quote(MoraleOption.PaySmall).Block);
    }

    [TestMethod]
    public void Quote_NoTroops_IsClosed()
    {
        _f.Party(new PartyBuilder { Troops = 0 });

        Assert.AreEqual(ProvenderBlock.NoTroops, _sut.Quote(MoraleOption.PaySmall).Block);
    }

    [TestMethod]
    public void Quote_EveryGate_ClosesEveryOption()
    {
        _f.Settings = _f.Settings with { Enabled = false };
        Assert.AreEqual(ProvenderBlock.MasterOff, _sut.Quote(MoraleOption.Drink).Block);

        _f.Settings = _f.Settings with { Enabled = true };
        _f.Coop.ShouldDeferToHost.Returns(true);
        Assert.AreEqual(ProvenderBlock.Coop, _sut.Quote(MoraleOption.PaySmall).Block);

        _f.Coop.ShouldDeferToHost.Returns(false);
        _f.Enlistment.IsEnlisted.Returns(true);
        Assert.AreEqual(ProvenderBlock.Enlisted, _sut.Quote(MoraleOption.PayLarge).Block);

        _f.Enlistment.IsEnlisted.Returns(false);
        _f.Party(new PartyBuilder { OfficerIsPlayer = true });
        Assert.AreEqual(ProvenderBlock.NoOfficer, _sut.Quote(MoraleOption.PaySmall).Block);
        Assert.IsFalse(_sut.Buy(MoraleOption.PaySmall));
        AssertNoMorale();
    }

    // ---- drink

    [TestMethod]
    public void Quote_Drink_AnElfHostIsGivenWine()
    {
        var p = new PartyBuilder { RaceCounts = new Dictionary<string, int> { ["elf"] = 30, ["human"] = 10 } };
        _f.Party(p);
        _f.Items.GetItemCount("wine").Returns(5);

        var q = _sut.Quote(MoraleOption.Drink);

        Assert.AreEqual("wine", q.ItemId);
        Assert.AreEqual(2, q.ItemCount);
        Assert.AreEqual(0, q.ToBuy);
        Assert.AreEqual(20, q.Morale);
    }

    [TestMethod]
    public void Quote_Drink_AnOrcHostIsGivenMeat()
    {
        _f.Party(new PartyBuilder { RaceCounts = new Dictionary<string, int> { ["orc"] = 40 } });
        _f.Items.GetItemCount("meat").Returns(100);

        var q = _sut.Quote(MoraleOption.Drink);

        Assert.AreEqual("meat", q.ItemId);
        Assert.AreEqual(8, q.ItemCount);
    }

    [TestMethod]
    public void Quote_Drink_NoPreferenceFallsBackToTheLeadersCultureThenTheDefault()
    {
        _f.Party(new PartyBuilder { LeaderCulture = "vlandia" });
        Assert.AreEqual("beer", _sut.Quote(MoraleOption.Drink).ItemId);

        _f.Party(new PartyBuilder { LeaderCulture = "gondor" });
        Assert.AreEqual("beer", _sut.Quote(MoraleOption.Drink).ItemId);
    }

    [TestMethod]
    public void Quote_Drink_AnEmptyCatalog_IsClosed()
    {
        _f.Rations.Get().Returns(RationCatalog.Empty);

        Assert.AreEqual(ProvenderBlock.NoRation, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Buy_Drink_FromTheStores_TakesTheItemsAndDisorganizes()
    {
        _f.Items.GetItemCount("beer").Returns(10);

        Assert.IsTrue(_sut.Buy(MoraleOption.Drink));

        _f.Items.Received(1).RemoveItem("beer", 6);
        _f.World.DidNotReceiveWithAnyArgs().Buy(default!, default!, default);
        _f.World.Received(1).AddMorale(15, true);
        _f.World.Received(1).Announce(ProvenderAnnouncement.Drank, 6, 0);
        _f.World.Received(1).CommitPartyScreen();
    }

    [TestMethod]
    public void Buy_Drink_TheShortfallIsBoughtAtTheMarketAndEarnsXp()
    {
        _f.Items.GetItemCount("beer").Returns(2);
        _f.Offer("beer", 25, 10);

        Assert.IsTrue(_sut.Buy(MoraleOption.Drink));

        _f.World.Received(1).Buy(Town, "beer", 4);
        _f.Items.Received(1).RemoveItem("beer", 6);
        _f.Xp.Received(1).AddSkillXp(Officer, "Trade", 1f);
        _f.World.Received(1).Announce(ProvenderAnnouncement.Drank, 6, 100);
    }

    [TestMethod]
    public void Quote_Drink_AShortfallInAFieldCamp_HasNoMarket()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = true });

        Assert.AreEqual(ProvenderBlock.NoMarket, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Quote_Drink_AShortfallInACastle_HasNoMarket()
    {
        _f.Settlement(new SettlementBuilder { IsTown = false, IsCastle = true });
        _f.Offer("beer", 25, 10);

        Assert.AreEqual(ProvenderBlock.NoMarket, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Quote_Drink_QuotesTheTownsRisingTotal_NotTheFirstPriceTimesTheCount()
    {
        _f.Items.GetItemCount("beer").Returns(2);
        _f.Offer("beer", 25, 10);
        _f.World.UnitPrices(Town, "beer", 4).Returns(new[] { 25, 30, 35, 40 });

        var q = _sut.Quote(MoraleOption.Drink);

        Assert.AreEqual(130, q.Gold);
        Assert.AreEqual(ProvenderBlock.None, q.Block);
    }

    [TestMethod]
    public void Quote_Drink_ARisingTotalAboveThePurse_IsClosedForGold()
    {
        // At the first price four beer would be 100 and pass a 120 purse; the town's real total is 130.
        _f.Party(new PartyBuilder { Gold = 120 });
        _f.Items.GetItemCount("beer").Returns(2);
        _f.Offer("beer", 25, 10);
        _f.World.UnitPrices(Town, "beer", 4).Returns(new[] { 25, 30, 35, 40 });

        Assert.AreEqual(ProvenderBlock.NoGold, _sut.Quote(MoraleOption.Drink).Block);
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void Quote_Drink_AShortfallInADisturbedHostileOrRefusingSettlement_HasNoMarket(bool disturbed, bool atWar, bool refuses)
    {
        _f.Settlement(new SettlementBuilder { IsTown = false, IsVillage = true, IsDisturbed = disturbed, AtWarWithPlayer = atWar, RefusesPlayer = refuses });
        _f.Offer("beer", 25, 10);

        Assert.AreEqual(ProvenderBlock.NoMarket, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Quote_Drink_TheMarketHoldsTooLittle_IsClosedForStock()
    {
        _f.Offer("beer", 25, 3);

        Assert.AreEqual(ProvenderBlock.NoStock, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Quote_Drink_TheShortfallCostsMoreThanTheGold_IsClosedForGold()
    {
        _f.Party(new PartyBuilder { Gold = 100 });
        _f.Offer("beer", 25, 10);

        Assert.AreEqual(ProvenderBlock.NoGold, _sut.Quote(MoraleOption.Drink).Block);
    }

    [TestMethod]
    public void Buy_Drink_TheMarketSellsShort_HandsNothingOut()
    {
        _f.Offer("beer", 25, 10);
        _f.World.Buy(Town, "beer", 6).Returns(new PurchaseResult(3, 75));

        Assert.IsFalse(_sut.Buy(MoraleOption.Drink));

        _f.Items.DidNotReceiveWithAnyArgs().RemoveItem(default!, default);
        AssertNoMorale();
    }

    [TestMethod]
    public void Buy_Drink_TheStoresGiveShort_PutsBackWhatWasTakenAndGivesNoMorale()
    {
        _f.Items.GetItemCount("beer").Returns(10);
        _f.Items.RemoveItem("beer", 6).Returns(4);

        Assert.IsFalse(_sut.Buy(MoraleOption.Drink));

        _f.Items.Received(1).AddItem("beer", 4);
        AssertNoMorale();
        _f.World.Received(1).CommitPartyScreen();
    }
}
