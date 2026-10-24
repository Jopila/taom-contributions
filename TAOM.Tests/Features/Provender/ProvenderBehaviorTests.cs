using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Provender.Hooks;
using TAOM.Features.Provender.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// What needs the game's types: the behavior's saved orders (a <c>CampaignBehaviorBase</c>) and the greyed reasons
/// (<c>TextObject</c>). The decisions the dialog lines call are in <see cref="ProvenderConversationFlowTests"/>, which runs
/// without the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ProvenderBehaviorTests
{
    private ProvenderFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new ProvenderFixture();

    private ProvenderBehavior Build() => new(_f.Service(), _f.World);

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void SyncData_SaveThenLoadIntoAnotherBehavior_RestoresBothOrders(bool autoFood, bool slaughter)
    {
        var first = Build();
        first.Orders.AutoFood = autoFood;
        first.Orders.Slaughter = slaughter;
        var store = new EngineDataStore();
        first.SyncData(store);

        var second = Build();
        store.Loading = true;
        second.SyncData(store);

        Assert.AreEqual(autoFood, second.Orders.AutoFood);
        Assert.AreEqual(slaughter, second.Orders.Slaughter);
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKeys_LeavesBothOrdersOff()
    {
        var sut = Build();
        sut.Orders.AutoFood = true;
        sut.Orders.Slaughter = true;

        sut.SyncData(new EngineDataStore { Loading = true });

        Assert.IsFalse(sut.Orders.AutoFood);
        Assert.IsFalse(sut.Orders.Slaughter);
    }

    [TestMethod]
    public void NewBehavior_StartsWithBothOrdersOff_WhateverAnEarlierCampaignHeld()
    {
        var earlier = Build();
        earlier.Orders.AutoFood = true;

        var later = Build();

        Assert.IsFalse(later.Orders.AutoFood);
        Assert.AreNotSame(earlier.Orders, later.Orders);
    }

    [TestMethod]
    public void BlockExplanation_IsNullWhenOpen_AndADistinctTextForEachReason()
    {
        Assert.IsNull(ProvenderDialogLines.BlockExplanation(ProvenderBlock.None));

        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var block in new[] { ProvenderBlock.NotInPlace, ProvenderBlock.NoTroops, ProvenderBlock.NoGold, ProvenderBlock.NoStock, ProvenderBlock.NoMarket })
        {
            var text = ProvenderDialogLines.BlockExplanation(block)!.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(text), block.ToString());
            Assert.IsTrue(seen.Add(text), block + " repeats another reason");
        }
        StringAssert.Contains(ProvenderDialogLines.BlockExplanation(ProvenderBlock.NoGold)!.ToString(), "gold");
    }
}
