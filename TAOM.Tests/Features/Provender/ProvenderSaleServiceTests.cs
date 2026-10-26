using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;
using static TAOM.Tests.Features.Provender.ProvenderFixture;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// Remote selling: which towns the Provender-master offers (the nearest eligible ones, never an ineligible town taking a
/// slot), the fee each carries, when the offer is closed, and the XP and message after the sale.
/// </summary>
[TestClass]
public class ProvenderSaleServiceTests
{
    private ProvenderFixture _f = null!;
    private IProvenderSaleAdapter _sale = null!;
    private IAlignmentService _alignment = null!;
    private ProvenderSaleService _sut = null!;
    private readonly SideIds _player = new("kingdom_player", "culture_player");
    private List<SaleTownFacts> _towns = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ProvenderFixture();
        _f.World.ConversationPartnerId.Returns(Officer);
        _sale = Substitute.For<IProvenderSaleAdapter>();
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Free);
        _sale.PlayerSide.Returns(_player);
        _towns = new List<SaleTownFacts> { Town("town_a", 10f), Town("town_b", 20f), Town("town_c", 30f), Town("town_d", 40f) };
        _sale.ReadTowns().Returns(_ => _towns);
        _sale.OpenScreen(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<Action<SaleOutcome>>()).Returns(true);
        _f.Xp.GetSkillValue(Officer, "Trade").Returns(0);
        _sut = new ProvenderSaleService(_sale, _f.World, _f.Service(), _f.SettingsProvider, _alignment, _f.Xp);
    }

    private static SaleTownFacts Town(string id, float distance) =>
        new() { Id = id, Name = "Name of " + id, Distance = distance, Gold = 20000, Side = new SideIds("kingdom_" + id, "culture_" + id) };

    private SaleTownFacts Change(string id, Func<SaleTownFacts, SaleTownFacts> edit)
    {
        var i = _towns.FindIndex(t => t.Id == id);
        _towns[i] = edit(_towns[i]);
        return _towns[i];
    }

    [TestMethod]
    public void Quote_OffersTheThreeNearestTowns_WithTheFeeRisingFivePerRank()
    {
        var q = _sut.Quote();

        Assert.AreEqual(ProvenderBlock.None, q.Block);
        CollectionAssert.AreEqual(new[] { "town_a", "town_b", "town_c" }, q.Offers.Select(o => o.TownId).ToArray());
        CollectionAssert.AreEqual(new[] { 30, 35, 40 }, q.Offers.Select(o => o.FeePercent).ToArray());
        Assert.AreEqual("Name of town_a", q.Offers[0].TownName);
    }

    [TestMethod]
    public void Quote_TheOfficersTradeLowersTheFee()
    {
        _f.Xp.GetSkillValue(Officer, "Trade").Returns(250);

        CollectionAssert.AreEqual(new[] { 10, 15, 20 }, _sut.Quote().Offers.Select(o => o.FeePercent).ToArray());
    }

    [TestMethod]
    public void Quote_TheTownCountComesFromTheSettings()
    {
        _f.Settings = _f.Settings with { SaleTownCount = 1 };

        Assert.AreEqual(1, _sut.Quote().Offers.Count);
    }

    [DataTestMethod]
    [DataRow("current")]
    [DataRow("war")]
    [DataRow("refuses")]
    [DataRow("siege")]
    [DataRow("poor")]
    public void Quote_AnIneligibleTown_IsSkipped_AndTheNextTakesItsSlot(string reason)
    {
        Change("town_a", t => reason switch
        {
            "current" => t with { IsCurrent = true },
            "war" => t with { AtWarWithPlayer = true },
            "refuses" => t with { RefusesPlayer = true },
            "siege" => t with { IsUnderSiege = true },
            _ => t with { Gold = ProvenderSaleRules.MinTownGold - 1 },
        });

        CollectionAssert.AreEqual(new[] { "town_b", "town_c", "town_d" }, _sut.Quote().Offers.Select(o => o.TownId).ToArray());
    }

    [TestMethod]
    public void Quote_ATownWithExactlyTheMinimumGold_IsOffered()
    {
        Change("town_a", t => t with { Gold = ProvenderSaleRules.MinTownGold });

        Assert.AreEqual("town_a", _sut.Quote().Offers[0].TownId);
    }

    [TestMethod]
    public void Quote_AnEnemyAlignedTown_IsSkipped()
    {
        _alignment.ResolveSide("kingdom_player", "culture_player").Returns(FactionSide.Free);
        _alignment.ResolveSide("kingdom_town_a", "culture_town_a").Returns(FactionSide.Evil);

        Assert.AreEqual("town_b", _sut.Quote().Offers[0].TownId);
    }

    [DataTestMethod]
    [DataRow(FactionSide.Neutral, FactionSide.Evil)]
    [DataRow(FactionSide.Free, FactionSide.Neutral)]
    [DataRow(FactionSide.Evil, FactionSide.Evil)]
    public void Quote_ANeutralSide_OrTheSameSide_IsNeverRefusedForAlignment(FactionSide player, FactionSide town)
    {
        _alignment.ResolveSide("kingdom_player", "culture_player").Returns(player);
        _alignment.ResolveSide("kingdom_town_a", "culture_town_a").Returns(town);

        Assert.AreEqual("town_a", _sut.Quote().Offers[0].TownId);
    }

    [TestMethod]
    public void Quote_NoEligibleTown_IsClosedForNoBuyer()
    {
        _towns.Clear();

        Assert.AreEqual(ProvenderBlock.NoBuyer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_OnTheOpenMap_IsClosed()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = false });

        Assert.AreEqual(ProvenderBlock.NotInPlace, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_InACampWithALiveEncounter_IsClosed_ThatPartyWouldTakeTheGold()
    {
        _f.Party(new PartyBuilder { SettlementId = null, CampReady = true, InEncounter = true });

        Assert.AreEqual(ProvenderBlock.InEncounter, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_InASettlement_AnEncounterIsFine()
    {
        _f.Party(new PartyBuilder { InEncounter = true });

        Assert.AreEqual(ProvenderBlock.None, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_TheTalkGateClosed_IsClosed()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.AreNotEqual(ProvenderBlock.None, _sut.Quote().Block);
        Assert.IsFalse(_sut.Open("town_a"));
        _sale.DidNotReceiveWithAnyArgs().OpenScreen(default!, default, default!);
    }

    [TestMethod]
    public void Open_OpensTheChosenTownAtItsFee()
    {
        Assert.IsTrue(_sut.Open("town_b"));

        _sale.Received(1).OpenScreen("town_b", 35, Arg.Any<Action<SaleOutcome>>());
    }

    [DataTestMethod]
    [DataRow("town_d")]
    [DataRow("nowhere")]
    [DataRow("")]
    [DataRow(null)]
    public void Open_ATownNotInTheFreshQuote_OpensNothing(string? townId)
    {
        Assert.IsFalse(_sut.Open(townId));

        _sale.DidNotReceiveWithAnyArgs().OpenScreen(default!, default, default!);
    }

    [TestMethod]
    public void Open_TheTownTheRowNamed_EvenWhenTheListShiftedSince()
    {
        // town_a dropped out after the rows were shown: the click still opens town_b, never the new first town.
        Change("town_a", t => t with { IsUnderSiege = true });

        Assert.IsTrue(_sut.Open("town_b"));

        _sale.Received(1).OpenScreen("town_b", 30, Arg.Any<Action<SaleOutcome>>());
    }

    [TestMethod]
    public void TheClosedSale_PaysTheOfficerTradeXp_AndAnnouncesIt()
    {
        Action<SaleOutcome>? onClosed = null;
        _sale.OpenScreen("town_a", 30, Arg.Do<Action<SaleOutcome>>(a => onClosed = a)).Returns(true);
        _sut.Open("town_a");

        onClosed!(new SaleOutcome(12, 850));

        _f.Xp.Received(1).AddSkillXp(Officer, "Trade", 8.5f);
        _f.World.Received(1).Announce(ProvenderAnnouncement.Sold, 12, 850);
    }

    [TestMethod]
    public void TheClosedSale_WithNothingSold_PaysNoXpAndSaysNothing()
    {
        Action<SaleOutcome>? onClosed = null;
        _sale.OpenScreen("town_a", 30, Arg.Do<Action<SaleOutcome>>(a => onClosed = a)).Returns(true);
        _sut.Open("town_a");

        onClosed!(new SaleOutcome(0, 0));

        _f.Xp.DidNotReceiveWithAnyArgs().AddSkillXp(default!, default!, default);
        _f.World.DidNotReceiveWithAnyArgs().Announce(default, default, default);
    }
}
