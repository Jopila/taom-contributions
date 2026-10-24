using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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
        _flow = new ProvenderConversationFlow(_f.Service(), _f.World);
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

    [TestMethod]
    public void ItemName_IsEmptyForAQuoteWithNoItem_AndTheAdaptersNameOtherwise()
    {
        _f.World.ItemName("beer").Returns("Ale");

        Assert.AreEqual(string.Empty, _flow.ItemName(string.Empty));
        Assert.AreEqual("Ale", _flow.ItemName("beer"));
    }
}
