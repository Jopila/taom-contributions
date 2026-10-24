using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;
using static TAOM.Tests.Features.Provender.ProvenderFixture;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The standing food orders: buying at a market and slaughtering livestock. Every skip state has its own test: the master
/// switch, the order itself, the officer (none, the player, out of the party), co-op, enlistment, and the settlement (a castle,
/// raided, under raid, at war, a village with no town, not found).
/// </summary>
[TestClass]
public class ProvenderServiceFoodTests
{
    private ProvenderFixture _f = null!;
    private ProvenderService _sut = null!;
    private readonly ProvenderOrders _both = new() { AutoFood = true, Slaughter = true };

    [TestInitialize]
    public void Setup()
    {
        _f = new ProvenderFixture();
        _sut = _f.Service();
    }

    private void AssertNoWrites()
    {
        _f.World.DidNotReceiveWithAnyArgs().Buy(default!, default!, default);
        _f.World.DidNotReceiveWithAnyArgs().Slaughter(default!, default);
        _f.Xp.DidNotReceiveWithAnyArgs().AddSkillXp(default!, default!, default);
        _f.World.DidNotReceiveWithAnyArgs().Announce(default, default, default);
    }

    // ---- buying food

    [TestMethod]
    public void TopUpFood_ValidScene_BuysEachTypeUpToTheTarget()
    {
        // 100 eaters over 2 stocked types: 50 of each. Grain has 50 in stock, cheese 30.
        var result = _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 50);
        _f.World.Received(1).Buy(Town, "cheese", 30);
        Assert.AreEqual(80, result.Units);
        Assert.AreEqual(50 * 10 + 30 * 20, result.Gold);
    }

    [TestMethod]
    public void TopUpFood_TheTargetDividesByTheTypesInStock_NotByTheTypesTheMarketLists()
    {
        var b = new SettlementBuilder();
        b.Offers = new List<FoodOffer> { new("grain", 10, 500), new("cheese", 20, 0), new("fish", 5, 0) };
        _f.Settlement(b);

        _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 100);
        _f.World.DidNotReceive().Buy(Town, "cheese", Arg.Any<int>());
    }

    [TestMethod]
    public void TopUpFood_FoodAlreadyHeld_BuysOnlyTheShortfall()
    {
        var p = new PartyBuilder();
        p.HeldFood["grain"] = 45;
        _f.Party(p);

        _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 5);
    }

    [TestMethod]
    public void TopUpFood_NotEnoughGold_BuysWhatTheGoldAffords()
    {
        var p = new PartyBuilder { Gold = 120 };
        _f.Party(p);

        var result = _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 12);
        _f.World.DidNotReceive().Buy(Town, "cheese", Arg.Any<int>());
        Assert.AreEqual(120, result.Gold);
    }

    [TestMethod]
    public void TopUpFood_PaysTheOfficerTradeAndStewardXp_OnTheGoldActuallySpent()
    {
        // The market stops the grain buy short: 4 units for 40 gold, not the 50 for 500 planned. The cheese buy lands whole.
        _f.World.Buy(Town, "grain", 50).Returns(new PurchaseResult(4, 40));
        _f.World.Buy(Town, "cheese", 30).Returns(new PurchaseResult(30, 600));

        var result = _sut.TopUpFood(_both, Town);

        Assert.AreEqual(640, result.Gold);
        Assert.AreEqual(34, result.Units);
        _f.Xp.Received(1).AddSkillXp(Officer, "Trade", 6.4f);
        _f.Xp.Received(1).AddSkillXp(Officer, "Steward", 3.2f);
    }

    [TestMethod]
    public void TopUpFood_NothingSpent_PaysNoXpAndShowsNoMessage()
    {
        _f.World.Buy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(PurchaseResult.None);

        var result = _sut.TopUpFood(_both, Town);

        Assert.AreEqual(PurchaseResult.None, result);
        _f.Xp.DidNotReceiveWithAnyArgs().AddSkillXp(default!, default!, default);
        _f.World.DidNotReceiveWithAnyArgs().Announce(default, default, default);
    }

    [TestMethod]
    public void TopUpFood_AMessageNamesTheUnitsAndTheGold()
    {
        _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Announce(ProvenderAnnouncement.BoughtFood, 80, 1100);
    }

    [TestMethod]
    public void TopUpFood_AllFoodHeldAlready_BuysNothing()
    {
        var p = new PartyBuilder();
        p.HeldFood["grain"] = 80;
        p.HeldFood["cheese"] = 80;
        _f.Party(p);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ASettlementWithNoFoodInStock_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { Offers = new List<FoodOffer>() });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_AVillageWithATown_Buys()
    {
        _f.Settlement(new SettlementBuilder { IsTown = false, IsVillage = true, HasMarketTown = true }, "village_1");

        var result = _sut.TopUpFood(_both, "village_1");

        Assert.IsTrue(result.Units > 0);
        _f.World.Received().Buy("village_1", "grain", 50);
    }

    [TestMethod]
    public void TopUpFood_AVillageWithNoTown_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { IsTown = false, IsVillage = true, HasMarketTown = false }, "village_1");

        _sut.TopUpFood(_both, "village_1");

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ACastle_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { IsTown = false, IsCastle = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ContradictoryCastleFacts_StillBuyNothing()
    {
        // Defensive: the engine never reports a castle as a town (Town.IsTown is !_isCastle), but the castle flag alone must
        // close the market even if an adapter got the two facts wrong.
        _f.Settlement(new SettlementBuilder { IsTown = true, IsCastle = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_AVillageNotInItsNormalState_BuysNothing()
    {
        // Raided, being raided, or forced for supplies or volunteers: vanilla's trade screen refuses all four.
        _f.Settlement(new SettlementBuilder { IsTown = false, IsVillage = true, IsDisturbed = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ASettlementThatRefusesThePlayer_BuysNothing()
    {
        // A neutral town that bars a player with a crime rating still raises SettlementEntered before its gate menu shows.
        _f.Settlement(new SettlementBuilder { RefusesPlayer = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_KeepsTheWageDaysOutOfTheBudget()
    {
        // 1000 gold, 200 a day in wages, three days kept: 400 to spend. Grain at 10 takes 40 of its 50, and none is left
        // for cheese.
        _f.Party(new PartyBuilder { Gold = 1000, DailyWage = 200 });

        var result = _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 40);
        _f.World.DidNotReceive().Buy(Town, "cheese", Arg.Any<int>());
        Assert.AreEqual(400, result.Gold);
    }

    [TestMethod]
    public void TopUpFood_AReserveAboveThePurse_BuysNothing()
    {
        _f.Party(new PartyBuilder { Gold = 500, DailyWage = 200 });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ALaterTypeIsPlannedAgainstTheGoldReallyLeft()
    {
        // Beer first at a rising 53, 59, 65, 75...: six planned at the first price (318) cut to four (252). The 66 left still
        // buys the six grain, which a plan made up front at first prices had no gold for.
        var b = new SettlementBuilder { Offers = new List<FoodOffer> { new("beer", 53, 50), new("grain", 10, 50) } };
        _f.Settlement(b);
        _f.Party(new PartyBuilder { Eaters = 12, Gold = 318 });
        var rising = new[] { 53, 59, 65, 75, 89, 113 };
        _f.World.UnitPrices(Town, "beer", Arg.Any<int>()).Returns(call => rising.Take(call.ArgAt<int>(2)).ToArray());
        _f.World.Buy(Town, "beer", 4).Returns(new PurchaseResult(4, 252));

        var result = _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "beer", 4);
        _f.World.Received(1).Buy(Town, "grain", 6);
        Assert.AreEqual(312, result.Gold);
    }

    [TestMethod]
    public void TopUpFood_ATypeTheGoldCannotMeet_IsSkipped_AndTheNextIsStillBought()
    {
        // Cheese at 500 a unit is out of a 100 budget; the grain after it is still bought.
        var b = new SettlementBuilder { Offers = new List<FoodOffer> { new("cheese", 500, 10), new("grain", 2, 100) } };
        _f.Settlement(b);
        _f.Party(new PartyBuilder { Eaters = 20, Gold = 100 });

        _sut.TopUpFood(_both, Town);

        _f.World.DidNotReceive().Buy(Town, "cheese", Arg.Any<int>());
        _f.World.Received(1).Buy(Town, "grain", 10);
    }

    [TestMethod]
    public void TopUpFood_ATypeTheMarketCannotPrice_IsSkipped_AndTheNextIsStillBought()
    {
        _f.World.UnitPrices(Town, "grain", Arg.Any<int>()).Returns(new int[0]);

        _sut.TopUpFood(_both, Town);

        _f.World.DidNotReceive().Buy(Town, "grain", Arg.Any<int>());
        _f.World.Received(1).Buy(Town, "cheese", 30);
    }

    [TestMethod]
    public void TopUpFood_ThePriceRisesWithEachUnit_BuysOnlyWhatTheBudgetCovers()
    {
        // A town raises the price as its stock falls. Unit k of the grain here costs 11 + 2k: 13 cost 299, 14 cost 336. The
        // purchase charges what the prices say, so the 1 gold left buys no cheese.
        _f.Party(new PartyBuilder { Gold = 300 });
        _f.World.UnitPrices(Town, "grain", Arg.Any<int>()).Returns(call => Enumerable.Range(0, call.ArgAt<int>(2)).Select(k => 11 + 2 * k).ToArray());
        _f.World.Buy(Town, "grain", 13).Returns(new PurchaseResult(13, 299));

        _sut.TopUpFood(_both, Town);

        _f.World.Received(1).Buy(Town, "grain", 13);
        _f.World.DidNotReceive().Buy(Town, "cheese", Arg.Any<int>());
    }

    [TestMethod]
    public void TopUpFood_ASettlementAtWarWithThePlayer_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { AtWarWithPlayer = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ASettlementThatIsNotFound_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { Found = false });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ASettlementThatIsNeitherTownNorVillage_BuysNothing()
    {
        _f.Settlement(new SettlementBuilder { IsTown = false });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_AnUnreadableSettlement_BuysNothing()
    {
        _f.World.ReadSettlement(Town).Returns(ProvenderSettlementFacts.None);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_TheOrderIsOff_BuysNothing()
    {
        _sut.TopUpFood(new ProvenderOrders { AutoFood = false, Slaughter = true }, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_TheMasterSwitchIsOff_BuysNothing()
    {
        _f.Settings = _f.Settings with { Enabled = false };

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_NoOfficer_BuysNothing()
    {
        _f.Party(new PartyBuilder { OfficerId = null });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_AnEmptyOfficerId_BuysNothing()
    {
        _f.Party(new PartyBuilder { OfficerId = "" });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_TheOfficerIsThePlayer_BuysNothing()
    {
        _f.Party(new PartyBuilder { OfficerIsPlayer = true });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_TheOfficerIsNotInTheParty_BuysNothing()
    {
        _f.Party(new PartyBuilder { OfficerInParty = false });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_NoParty_BuysNothing()
    {
        _f.World.ReadParty().Returns(ProvenderPartyFacts.None);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_CoopDefersToTheHost_BuysNothing()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_OnADedicatedServer_BuysNothing()
    {
        // A single-player save with the order on, hosted on a dedicated server, must not feed the server's idle hero.
        _f.Server.IsDedicatedServer.Returns(true);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_ThePlayerIsEnlisted_BuysNothing()
    {
        _f.Enlistment.IsEnlisted.Returns(true);

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    [TestMethod]
    public void TopUpFood_NoEaters_BuysNothing()
    {
        _f.Party(new PartyBuilder { Eaters = 0 });

        _sut.TopUpFood(_both, Town);

        AssertNoWrites();
    }

    // ---- slaughter

    private void LowFoodWithSheep(int sheep = 10)
    {
        var p = new PartyBuilder { EdibleFood = 5f, FoodPerDay = 5f };
        p.Livestock.Add(new LivestockStack("sheep", sheep, 2));
        _f.Party(p);
    }

    [TestMethod]
    public void DailySlaughter_FoodBelowTheMark_SlaughtersUntilTheBufferIsBack()
    {
        // 5 food a day: a sheep is two meat, so 0.4 days. From 1 day, three days wanted: five heads reach 3.0.
        LowFoodWithSheep();

        var heads = _sut.DailySlaughter(_both);

        Assert.AreEqual(5, heads);
        _f.World.Received(1).Slaughter("sheep", 5);
    }

    [TestMethod]
    public void DailySlaughter_PaysTheOfficerTenStewardXpPerHeadActuallySlaughtered()
    {
        LowFoodWithSheep();
        _f.World.Slaughter("sheep", 5).Returns(3);

        var heads = _sut.DailySlaughter(_both);

        Assert.AreEqual(3, heads);
        _f.Xp.Received(1).AddSkillXp(Officer, "Steward", 30f);
        _f.Xp.DidNotReceive().AddSkillXp(Officer, "Trade", Arg.Any<float>());
        _f.World.Received(1).Announce(ProvenderAnnouncement.Slaughtered, 3, 0);
    }

    [TestMethod]
    public void DailySlaughter_KeepsTheConfiguredHeadsOfEachType()
    {
        LowFoodWithSheep(3);

        _sut.DailySlaughter(_both);

        _f.World.Received(1).Slaughter("sheep", 1);
    }

    [TestMethod]
    public void DailySlaughter_FoodAtTheMark_SlaughtersNothing()
    {
        var p = new PartyBuilder { EdibleFood = 15f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_TheLowMarkComesFromTheSettings()
    {
        var p = new PartyBuilder { EdibleFood = 15f, FoodPerDay = 5f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);
        _f.Settings = _f.Settings with { LowFoodDays = 4 };

        _sut.DailySlaughter(_both);

        _f.World.Received(1).Slaughter("sheep", 3);
    }

    [TestMethod]
    public void DailySlaughter_TheKeepLevelComesFromTheSettings()
    {
        LowFoodWithSheep(10);
        _f.Settings = _f.Settings with { KeepPerType = 8 };

        _sut.DailySlaughter(_both);

        _f.World.Received(1).Slaughter("sheep", 2);
    }

    [TestMethod]
    public void DailySlaughter_AFoodBurnOfZero_SlaughtersNothing()
    {
        var p = new PartyBuilder { EdibleFood = 0f, FoodPerDay = 0f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_NoLivestock_SlaughtersNothing()
    {
        _f.Party(new PartyBuilder { EdibleFood = 0f });

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_TheOrderIsOff_SlaughtersNothing()
    {
        LowFoodWithSheep();

        _sut.DailySlaughter(new ProvenderOrders { AutoFood = true, Slaughter = false });

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_TheMasterSwitchIsOff_SlaughtersNothing()
    {
        LowFoodWithSheep();
        _f.Settings = _f.Settings with { Enabled = false };

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_NoOfficer_SlaughtersNothing()
    {
        var p = new PartyBuilder { OfficerId = null, EdibleFood = 0f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_TheOfficerIsThePlayer_SlaughtersNothing()
    {
        var p = new PartyBuilder { OfficerIsPlayer = true, EdibleFood = 0f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_TheOfficerIsNotInTheParty_SlaughtersNothing()
    {
        var p = new PartyBuilder { OfficerInParty = false, EdibleFood = 0f };
        p.Livestock.Add(new LivestockStack("sheep", 10, 2));
        _f.Party(p);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_CoopDefersToTheHost_SlaughtersNothing()
    {
        LowFoodWithSheep();
        _f.Coop.ShouldDeferToHost.Returns(true);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_OnADedicatedServer_SlaughtersNothing()
    {
        LowFoodWithSheep();
        _f.Server.IsDedicatedServer.Returns(true);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_ThePlayerIsEnlisted_SlaughtersNothing()
    {
        LowFoodWithSheep();
        _f.Enlistment.IsEnlisted.Returns(true);

        _sut.DailySlaughter(_both);

        AssertNoWrites();
    }

    [TestMethod]
    public void DailySlaughter_NothingSlaughtered_PaysNoXpAndShowsNoMessage()
    {
        LowFoodWithSheep();
        _f.World.Slaughter(Arg.Any<string>(), Arg.Any<int>()).Returns(0);

        var heads = _sut.DailySlaughter(_both);

        Assert.AreEqual(0, heads);
        _f.Xp.DidNotReceiveWithAnyArgs().AddSkillXp(default!, default!, default);
        _f.World.DidNotReceiveWithAnyArgs().Announce(default, default, default);
    }

    // ---- who may talk

    [TestMethod]
    public void CanTalk_TheConversationPartnerIsTheOfficer_IsTrue()
    {
        Assert.IsTrue(_sut.CanTalk(Officer));
    }

    [TestMethod]
    public void CanTalk_AnotherHero_NeverReadsTheParty()
    {
        _f.World.ClearReceivedCalls();

        Assert.IsFalse(_sut.CanTalk("lord_other"));

        _f.World.DidNotReceive().ReadParty();
    }

    [TestMethod]
    public void CanTalk_AnotherHero_OrNoOne_IsFalse()
    {
        Assert.IsFalse(_sut.CanTalk("lord_other"));
        Assert.IsFalse(_sut.CanTalk(null));
        Assert.IsFalse(_sut.CanTalk(""));
    }

    [TestMethod]
    public void CanTalk_EveryGateClosed_IsFalse()
    {
        _f.Settings = _f.Settings with { Enabled = false };
        Assert.IsFalse(_sut.CanTalk(Officer), "master off");

        _f.Settings = _f.Settings with { Enabled = true };
        _f.Coop.ShouldDeferToHost.Returns(true);
        Assert.IsFalse(_sut.CanTalk(Officer), "co-op");

        _f.Coop.ShouldDeferToHost.Returns(false);
        _f.Server.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_sut.CanTalk(Officer), "dedicated server");

        _f.Server.IsDedicatedServer.Returns(false);
        _f.Enlistment.IsEnlisted.Returns(true);
        Assert.IsFalse(_sut.CanTalk(Officer), "enlisted");

        _f.Enlistment.IsEnlisted.Returns(false);
        _f.Party(new PartyBuilder { OfficerIsPlayer = true });
        Assert.IsFalse(_sut.CanTalk(Officer), "officer is the player");

        _f.Party(new PartyBuilder { OfficerInParty = false });
        Assert.IsFalse(_sut.CanTalk(Officer), "officer out of the party");
    }
}
