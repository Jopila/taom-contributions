using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.SettlementServices;

namespace TAOM.Tests.Features.SettlementServices;

[TestClass]
public class WatchSilverServiceTests
{
    private ISettlementServicesAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private TaomSettings _settings = null!;
    private WatchSilverService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ISettlementServicesAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings = new TaomSettings();
        _adapter.PlayerGold.Returns(1_000_000);
        _adapter.ChargePlayer(Arg.Any<int>()).Returns(ci => ci.Arg<int>());
        _adapter.SetLoyalty(Arg.Any<string>(), Arg.Any<float>()).Returns(ci => ci.ArgAt<float>(1));
        Fief();
        _sut = new WatchSilverService(_adapter, _coop, _dedicated, new SettlementServicesSettingsProvider(_settings), _logger);
    }

    private void Fief(float loyalty = 32f, float militia = 120f, bool owned = true, bool governorIn = true,
        bool playerIn = true, string id = "town_A") =>
        _adapter.ReadGovernedFief().Returns(new GovernedFiefSnapshot(id, owned, governorIn, playerIn, loyalty, militia));

    private static int[] Points(WatchSilverQuote quote) => quote.Tiers.Select(t => t.Points).ToArray();

    private static int[] Prices(WatchSilverQuote quote) => quote.Tiers.Select(t => t.Price).ToArray();

    // --- The gates, in order --------------------------------------------------------------------------------------

    [TestMethod]
    public void Quote_AllClear_IsNone()
        => Assert.AreEqual(WatchSilverBlock.None, _sut.Quote().Block);

    [TestMethod]
    public void Quote_SwitchOff_IsDisabled()
    {
        _settings.EnableWatchSilver = false;

        Assert.AreEqual(WatchSilverBlock.Disabled, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_SwitchOff_WinsOverEveryOtherGate()
    {
        _settings.EnableWatchSilver = false;
        _coop.IsSessionActive.Returns(true);
        _adapter.ReadGovernedFief().Returns((GovernedFiefSnapshot?)null);

        Assert.AreEqual(WatchSilverBlock.Disabled, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_ACoopSessionIsLive_IsNotSinglePlayer()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_TheCoopModIsUnprobeable_IsNotSinglePlayer()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_OnADedicatedServer_IsNotSinglePlayer()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_ThisPeerIsNotTheAuthority_IsNotSinglePlayer()
    {
        _coop.IsAuthority.Returns(false);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_NotSinglePlayer_WinsOverNotEligible()
    {
        _coop.IsSessionActive.Returns(true);
        _adapter.ReadGovernedFief().Returns((GovernedFiefSnapshot?)null);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_TheHeroGovernsNothing_IsNotEligible()
    {
        _adapter.ReadGovernedFief().Returns((GovernedFiefSnapshot?)null);

        var quote = _sut.Quote();

        Assert.AreEqual(WatchSilverBlock.NotEligible, quote.Block);
        Assert.AreEqual(0, quote.Tiers.Count);
    }

    [TestMethod]
    public void Quote_AnotherClansFief_IsNotEligible()
    {
        Fief(owned: false);

        Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_TheGovernorIsAwayFromTheFief_IsNotEligible()
    {
        Fief(governorIn: false);

        Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_ThePlayerIsAwayFromTheFief_IsNotEligible()
    {
        Fief(playerIn: false);

        Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_NotEligible_WinsOverAProblem()
    {
        Fief(loyalty: float.NaN, owned: false);

        Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_LoyaltyNaN_IsAProblem_NotAnOffer()
    {
        Fief(loyalty: float.NaN);

        var quote = _sut.Quote();

        Assert.AreEqual(WatchSilverBlock.Problem, quote.Block);
        Assert.AreEqual(0, quote.Tiers.Count);
    }

    [TestMethod]
    public void Quote_LoyaltyPositiveInfinity_IsAProblem()
    {
        Fief(loyalty: float.PositiveInfinity);

        Assert.AreEqual(WatchSilverBlock.Problem, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_LoyaltyNegativeInfinity_IsAProblem()
    {
        Fief(loyalty: float.NegativeInfinity);

        Assert.AreEqual(WatchSilverBlock.Problem, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_MilitiaNaN_IsAProblem()
    {
        Fief(militia: float.NaN);

        Assert.AreEqual(WatchSilverBlock.Problem, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_MilitiaInfinity_IsAProblem()
    {
        Fief(militia: float.PositiveInfinity);

        Assert.AreEqual(WatchSilverBlock.Problem, _sut.Quote().Block);
    }

    [TestMethod]
    public void Quote_AProblem_IsLoggedOnce_NotOnEveryCondition()
    {
        Fief(loyalty: float.NaN);

        _sut.Quote();
        _sut.Quote();
        _sut.Quote();

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void IsOffered_IsTrueForNoneAndAtCeilingOnly_ForEveryBlock()
    {
        foreach (var block in Enum.GetValues(typeof(WatchSilverBlock)).Cast<WatchSilverBlock>())
        {
            var quote = new WatchSilverQuote(block, 0f, Array.Empty<WatchSilverTier>());

            Assert.AreEqual(block == WatchSilverBlock.None || block == WatchSilverBlock.AtCeiling, quote.IsOffered, block.ToString());
        }
    }

    [TestMethod]
    public void Quote_AProblem_IsLoggedOncePerFief()
    {
        Fief(loyalty: float.NaN, id: "town_A");
        _sut.Quote();
        _sut.Quote();
        Fief(loyalty: float.NaN, id: "town_B");
        _sut.Quote();
        _sut.Quote();

        _logger.Received(2).LogError(Arg.Any<string>());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("town_B")));
    }

    [TestMethod]
    public void Quote_LoyaltyJustBelowTheCeilingGap_IsAtCeiling()
    {
        Fief(loyalty: 49.5f);

        var quote = _sut.Quote();

        Assert.AreEqual(WatchSilverBlock.AtCeiling, quote.Block);
        Assert.AreEqual(0, quote.Tiers.Count);
    }

    [TestMethod]
    public void Quote_LoyaltyAtOrPastTheCeiling_IsAtCeiling()
    {
        foreach (var loyalty in new[] { 50f, 50.5f, 75f, 100f })
        {
            Fief(loyalty: loyalty);

            Assert.AreEqual(WatchSilverBlock.AtCeiling, _sut.Quote().Block, loyalty.ToString());
        }
    }

    [TestMethod]
    public void Quote_ExactlyOnePointBelowTheCeiling_OffersOneRowOfOne()
    {
        Fief(loyalty: 49f);

        var quote = _sut.Quote();

        Assert.AreEqual(WatchSilverBlock.None, quote.Block);
        CollectionAssert.AreEqual(new[] { 1 }, Points(quote));
    }

    // --- Points and clipping --------------------------------------------------------------------------------------

    [TestMethod]
    public void Quote_FarFromTheCeiling_OffersThePlainTiers()
    {
        Fief(loyalty: 10f);

        CollectionAssert.AreEqual(new[] { 5, 10, 20 }, Points(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_NearTheCeiling_ClipsTheLastTierToTheGap()
    {
        Fief(loyalty: 32f);

        CollectionAssert.AreEqual(new[] { 5, 10, 18 }, Points(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AFractionalGap_FloorsIt_AndNeverShowsTwoEqualRows()
    {
        Fief(loyalty: 47.3f);

        CollectionAssert.AreEqual(new[] { 2 }, Points(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AGapBetweenTwoTiers_ShowsTheClippedRowOnce()
    {
        Fief(loyalty: 42.8f);

        CollectionAssert.AreEqual(new[] { 5, 7 }, Points(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_NeverPassesTheCeiling_ForAnyLoyalty()
    {
        for (var loyalty = 0f; loyalty < 50f; loyalty += 0.7f)
        {
            Fief(loyalty: loyalty);
            var quote = _sut.Quote();
            if (quote.Block != WatchSilverBlock.None)
                continue;
            foreach (var tier in quote.Tiers)
                Assert.IsTrue(loyalty + tier.Points <= WatchSilverService.Ceiling, $"loyalty {loyalty} + {tier.Points}");
        }
    }

    [TestMethod]
    public void Quote_CarriesTheCurrentLoyalty_ForTheGovernorsLine()
    {
        Fief(loyalty: 32.5f);

        Assert.AreEqual(32.5f, _sut.Quote().Loyalty);
    }

    // --- Price ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Quote_TheWorkedExample_PricesFourDenarsPerMilitiaPerPoint()
    {
        Fief(loyalty: 32f, militia: 120f);

        CollectionAssert.AreEqual(new[] { 2400, 4800, 8640 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_ASmallMilitia_PricesAtTheFloor()
    {
        Fief(loyalty: 10f, militia: 30f);

        CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AMilitiaOfZero_PricesAtTheFloor_NeverFree()
    {
        Fief(loyalty: 10f, militia: 0f);

        CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_ANegativeMilitia_PricesAtTheFloor()
    {
        Fief(loyalty: 10f, militia: -5f);

        CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AMilitiaOnTheFloor_PricesAsItself()
    {
        Fief(loyalty: 10f, militia: 50f);

        CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AFractionalProduct_RoundsUp()
    {
        Fief(loyalty: 49f, militia: 50.3f);

        // 1 point * 50.3 * 4 = 201.2
        CollectionAssert.AreEqual(new[] { 202 }, Prices(_sut.Quote()));
    }

    [TestMethod]
    public void Quote_AHugeMilitia_ClampsToTheLargestInt_WithoutThrowing()
    {
        Fief(loyalty: 10f, militia: 3e38f);

        var quote = _sut.Quote();

        Assert.AreEqual(WatchSilverBlock.None, quote.Block);
        CollectionAssert.AreEqual(new[] { int.MaxValue, int.MaxValue, int.MaxValue }, Prices(quote));
    }

    [TestMethod]
    public void CanAfford_IsTrueFromExactlyThePrice()
    {
        _adapter.PlayerGold.Returns(2400);

        Assert.IsTrue(_sut.CanAfford(2400));
        Assert.IsFalse(_sut.CanAfford(2401));
    }

    // --- Paying ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Apply_ChargesThePrice_ThenSetsTheAbsoluteLoyalty()
    {
        var result = _sut.Apply(0);

        Assert.IsTrue(result.Success);
        Received.InOrder(() =>
        {
            _adapter.ChargePlayer(2400);
            _adapter.SetLoyalty("town_A", 37f);
        });
    }

    [TestMethod]
    public void Apply_TheLastTier_ReachesTheCeilingNoFurther()
    {
        var result = _sut.Apply(2);

        _adapter.Received(1).ChargePlayer(8640);
        _adapter.Received(1).SetLoyalty("town_A", 50f);
        Assert.AreEqual(50f, result.Loyalty);
    }

    [TestMethod]
    public void Apply_ReportsTheReadBackLoyalty_NotTheRequestedValue()
    {
        _adapter.SetLoyalty("town_A", 37f).Returns(36.5f);

        var result = _sut.Apply(0);

        Assert.AreEqual(WatchSilverBlock.None, result.Block);
        Assert.AreEqual(36.5f, result.Loyalty);
    }

    [TestMethod]
    public void Apply_ShortOfGold_ChargesNothingAndWritesNothing()
    {
        _adapter.PlayerGold.Returns(2399);

        var result = _sut.Apply(0);

        Assert.AreEqual(WatchSilverBlock.NotEnoughGold, result.Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
        _adapter.DidNotReceive().SetLoyalty(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Apply_ExactlyTheGold_Pays()
    {
        _adapter.PlayerGold.Returns(2400);

        Assert.IsTrue(_sut.Apply(0).Success);
    }

    [TestMethod]
    public void Apply_AThrowingSetLoyalty_RefundsThePrice()
    {
        _adapter.SetLoyalty(Arg.Any<string>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));

        var result = _sut.Apply(0);

        Assert.AreEqual(WatchSilverBlock.Problem, result.Block);
        _adapter.Received(1).Refund(2400);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_AChargeThatThrowsAfterTheDebit_RefundsWhatLeft()
    {
        var gold = 10_000;
        _adapter.PlayerGold.Returns(ci => gold);
        _adapter.When(a => a.ChargePlayer(2400)).Do(_ =>
        {
            gold -= 2400;
            throw new InvalidOperationException("dispatch failed after the debit");
        });
        _adapter.When(a => a.Refund(Arg.Any<int>())).Do(ci => gold += ci.Arg<int>());

        var result = _sut.Apply(0);

        Assert.AreEqual(WatchSilverBlock.Problem, result.Block);
        Assert.AreEqual(10_000, gold, "what the debit took is given back");
        _adapter.Received(1).Refund(2400);
        _adapter.DidNotReceive().SetLoyalty(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Apply_ReReadsEveryGate_SwitchOff()
    {
        _settings.EnableWatchSilver = false;

        Assert.AreEqual(WatchSilverBlock.Disabled, _sut.Apply(0).Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Apply_ReReadsEveryGate_Coop()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(WatchSilverBlock.NotSinglePlayer, _sut.Apply(0).Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Apply_ReReadsEveryGate_TheFiefIsNotTheirs()
    {
        Fief(owned: false);

        Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Apply(0).Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Apply_AtTheCeiling_ChargesNothing()
    {
        Fief(loyalty: 50f);

        Assert.AreEqual(WatchSilverBlock.AtCeiling, _sut.Apply(0).Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Apply_ATierThatIsNotOffered_IsRefused_AndChargesNothing()
    {
        Fief(loyalty: 47.3f);

        foreach (var index in new[] { -1, 1, 2, 3 })
            Assert.AreEqual(WatchSilverBlock.NotEligible, _sut.Apply(index).Block, index.ToString());
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Apply_UsesTheLivePrice_NotAStaleQuote()
    {
        var before = _sut.Quote();
        Fief(loyalty: 32f, militia: 200f);

        _sut.Apply(0);

        Assert.AreEqual(2400, before.Tiers[0].Price);
        _adapter.Received(1).ChargePlayer(4000);
    }
}
