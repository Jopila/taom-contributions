using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using TAOM.Features.Execution;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;
using static TAOM.Tests.Features.Provender.ProvenderFixture;

namespace TAOM.Tests.Features.Provender;

/// <summary>The conversation hand-offs: who may talk, the order toggles and the morale purchase the officer answers.</summary>
[TestClass]
public class ProvenderConversationFlowTests
{
    private ProvenderFixture _f = null!;
    private ProvenderConversationFlow _flow = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ProvenderFixture();
        _flow = new ProvenderConversationFlow(_f.Service(), _f.SaleService(), _f.World);
    }

    [TestMethod]
    public void CanTalk_OnlyWithTheOfficer()
    {
        _f.World.ConversationPartnerId.Returns(Officer);
        Assert.IsTrue(_flow.CanTalk());

        _f.World.ConversationPartnerId.Returns("lord_other");
        Assert.IsFalse(_flow.CanTalk());

        _f.World.ConversationPartnerId.Returns((string?)null);
        Assert.IsFalse(_flow.CanTalk());
    }

    [TestMethod]
    public void ANewFlow_StartsWithBothOrdersOff()
    {
        Assert.IsTrue(_flow.FoodOrderIsOff());
        Assert.IsTrue(_flow.SlaughterOrderIsOff());
        Assert.IsFalse(_flow.FoodOrderIsOn());
        Assert.IsFalse(_flow.SlaughterOrderIsOn());
    }

    [TestMethod]
    public void TheToggles_SetTheirOwnOrderOnly()
    {
        _f.World.ConversationPartnerId.Returns(Officer);
        _flow.SetFoodOrder(true);
        Assert.IsTrue(_flow.Orders.AutoFood);
        Assert.IsFalse(_flow.Orders.Slaughter);

        _flow.SetSlaughterOrder(true);
        _flow.SetFoodOrder(false);
        Assert.IsFalse(_flow.Orders.AutoFood);
        Assert.IsTrue(_flow.Orders.Slaughter);
    }

    [TestMethod]
    public void TheToggles_DoNothing_WhenTheGateClosedAfterTheLineWasShown()
    {
        _f.World.ConversationPartnerId.Returns(Officer);
        _f.Coop.ShouldDeferToHost.Returns(true);

        _flow.SetFoodOrder(true);
        _flow.SetSlaughterOrder(true);

        Assert.IsFalse(_flow.Orders.AutoFood);
        Assert.IsFalse(_flow.Orders.Slaughter);
    }

    [TestMethod]
    public void Buy_RecordsWhetherTheSpiritsWereLifted()
    {
        _flow.Buy(MoraleOption.PaySmall);
        Assert.IsTrue(_flow.LastBuyLifted);

        _f.Party(new PartyBuilder { Gold = 0 });
        _flow.Buy(MoraleOption.PaySmall);
        Assert.IsFalse(_flow.LastBuyLifted);
    }

    private void TwoTownsOnSale()
    {
        _f.World.ConversationPartnerId.Returns(Officer);
        _f.Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Neutral);
        _f.Sale.PlayerSide.Returns(SideIds.None);
        _f.Sale.ReadTowns().Returns(new[]
        {
            new SaleTownFacts { Id = "town_a", Name = "A", Distance = 1f, Gold = 9000 },
            new SaleTownFacts { Id = "town_b", Name = "B", Distance = 2f, Gold = 9000 },
        });
        _f.Sale.OpenScreen(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<Action<SaleOutcome>>()).Returns(true);
    }

    [TestMethod]
    public void PrepareSale_TakesTheQuoteOnce_AndTheRowsReadIt()
    {
        TwoTownsOnSale();

        Assert.IsNull(_flow.SaleOfferAt(0), "no quote is taken before the player asks");
        _flow.PrepareSale();

        Assert.AreEqual("town_a", _flow.SaleOfferAt(0)!.TownId);
        Assert.AreEqual("town_b", _flow.SaleOfferAt(1)!.TownId);
        Assert.IsNull(_flow.SaleOfferAt(2));
        Assert.IsNull(_flow.SaleOfferAt(-1));
        _f.Sale.Received(1).ReadTowns();
    }

    [TestMethod]
    public void Sell_RecordsWhetherTheScreenWillOpen()
    {
        TwoTownsOnSale();

        _flow.PrepareSale();
        _flow.Sell(1);
        Assert.IsTrue(_flow.LastSaleOpened);
        _f.Sale.Received(1).OpenScreen("town_b", Arg.Any<int>(), Arg.Any<Action<SaleOutcome>>());

        _flow.Sell(5);
        Assert.IsFalse(_flow.LastSaleOpened, "a rank the shown list does not hold opens nothing");
    }

    [TestMethod]
    public void ItemName_IsEmptyForAQuoteWithNoItem_AndTheAdaptersNameOtherwise()
    {
        _f.World.ItemName("beer").Returns("Ale");

        Assert.AreEqual(string.Empty, _flow.ItemName(string.Empty));
        Assert.AreEqual("Ale", _flow.ItemName("beer"));
    }
}
