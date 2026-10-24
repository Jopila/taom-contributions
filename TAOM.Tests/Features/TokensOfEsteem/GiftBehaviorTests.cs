using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.TokensOfEsteem.Hooks;
using TAOM.Features.TokensOfEsteem.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// What needs the game's types: the behavior's saved ledger (a <c>CampaignBehaviorBase</c>) and the two explanation texts
/// (<c>TextObject</c>). The flow the dialog lines call is <see cref="GiftConversationFlowTests"/>, which runs without the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class GiftBehaviorTests
{
    private GiftFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GiftFixture();

    private GiftBehavior Build() => new(_f.Sut, Substitute.For<IGiftConversationAdapter>(), Substitute.For<ICoopSessionProvider>(), Substitute.For<TAOM.Features.Messengers.IMessengerConversationState>(), _f.Logger);

    [TestMethod]
    public void SyncData_SaveThenLoadIntoAnotherBehavior_RestoresTheLedger()
    {
        var first = Build();
        first.Ledger.Stamp("lord_1", 40);
        first.Ledger.Stamp("companion_9", 63);
        var store = new EngineDataStore();
        first.SyncData(store);

        var second = Build();
        store.Loading = true;
        second.SyncData(store);

        Assert.AreEqual(40, second.Ledger.LastGiftDay("lord_1"));
        Assert.AreEqual(63, second.Ledger.LastGiftDay("companion_9"));
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKey_YieldsAnEmptyLedger()
    {
        var sut = Build();
        sut.Ledger.Stamp("leftover", 5);

        sut.SyncData(new EngineDataStore { Loading = true });

        Assert.IsNull(sut.Ledger.LastGiftDay("leftover"));
    }

    [TestMethod]
    public void SyncData_DamagedEntries_AreDroppedWithAWarning()
    {
        var sut = Build();
        var store = new EngineDataStore();
        var broken = new Dictionary<string, string> { ["v"] = "1", ["last|ok"] = "4", ["last|bad"] = "x" };
        store.SyncData(GiftBehavior.SaveKey, ref broken);
        store.Loading = true;

        sut.SyncData(store);

        Assert.AreEqual(4, sut.Ledger.LastGiftDay("ok"));
        Assert.IsNull(sut.Ledger.LastGiftDay("bad"));
        _f.Logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void NewBehavior_StartsWithAnEmptyLedger_WhateverAnEarlierCampaignHeld()
    {
        var earlier = Build();
        earlier.Ledger.Stamp(GiftFixture.HeroId, 99);

        var later = Build();

        Assert.IsNull(later.Ledger.LastGiftDay(GiftFixture.HeroId));
        Assert.AreNotSame(earlier.Ledger, later.Ledger);
    }

    [TestMethod]
    public void CooldownExplanation_NamesTheDaysLeft()
    {
        var text = GiftDialogLines.CooldownExplanation(5).ToString();

        StringAssert.Contains(text, "5");
        Assert.IsFalse(text.Contains("{DAYS}"), "the variable is filled");
    }

    [TestMethod]
    public void BlockExplanation_DiffersForGoldAndForNoEffect()
    {
        var gold = GiftDialogLines.BlockExplanation(GiftBlock.CannotAfford).ToString();
        var none = GiftDialogLines.BlockExplanation(GiftBlock.NoEffect).ToString();

        Assert.AreNotEqual(gold, none);
        StringAssert.Contains(gold, "gold");
    }
}
