using System;
using System.Collections.Generic;
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
public class TidingsServiceTests
{
    private const float Cutoff = 5000f;

    private ISettlementServicesAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private TaomSettings _settings = null!;
    private TidingsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ISettlementServicesAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings = new TaomSettings();
        _adapter.PlayerGold.Returns(500);
        _adapter.TournamentDistanceCutoff.Returns(Cutoff);
        _adapter.ChargePlayer(Arg.Any<int>()).Returns(ci => ci.Arg<int>());
        Sites();
        _sut = new TidingsService(_adapter, _coop, _dedicated, new SettlementServicesSettingsProvider(_settings), _logger);
    }

    private static TournamentSiteSnapshot Site(string id, float distance, bool war = false, bool current = false) =>
        new(id, "Town " + id, "Realm " + id, distance, war, current);

    private void Sites(params TournamentSiteSnapshot[] sites) =>
        _adapter.ReadTournamentSites().Returns((IReadOnlyList<TournamentSiteSnapshot>)sites);

    private static string[] Towns(TidingsResult result) => result.Rows.Select(r => r.Town).ToArray();

    // --- Check: hidden or greyed ----------------------------------------------------------------------------------

    [TestMethod]
    public void Check_AllClear_IsNone()
        => Assert.AreEqual(TidingsBlock.None, _sut.Check());

    [TestMethod]
    public void Check_SwitchOff_IsDisabled()
    {
        _settings.EnableTidingsOfTheLists = false;

        Assert.AreEqual(TidingsBlock.Disabled, _sut.Check());
    }

    [TestMethod]
    public void Check_ACoopSessionIsLive_IsNotSinglePlayer()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(TidingsBlock.NotSinglePlayer, _sut.Check());
    }

    [TestMethod]
    public void Check_TheCoopModIsUnprobeable_IsNotSinglePlayer()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(TidingsBlock.NotSinglePlayer, _sut.Check());
    }

    [TestMethod]
    public void Check_OnADedicatedServer_IsNotSinglePlayer()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.AreEqual(TidingsBlock.NotSinglePlayer, _sut.Check());
    }

    [TestMethod]
    public void Check_ThisPeerIsNotTheAuthority_IsNotSinglePlayer()
    {
        _coop.IsAuthority.Returns(false);

        Assert.AreEqual(TidingsBlock.NotSinglePlayer, _sut.Check());
    }

    [TestMethod]
    public void Check_Disguised_IsDisguised()
    {
        _adapter.IsPlayerDisguised.Returns(true);

        Assert.AreEqual(TidingsBlock.Disguised, _sut.Check());
    }

    [TestMethod]
    public void Check_OneDenarShort_IsNotEnoughGold()
    {
        _adapter.PlayerGold.Returns(TidingsService.Fee - 1);

        Assert.AreEqual(TidingsBlock.NotEnoughGold, _sut.Check());
    }

    [TestMethod]
    public void Check_ExactlyTheFee_IsNone()
    {
        _adapter.PlayerGold.Returns(TidingsService.Fee);

        Assert.AreEqual(TidingsBlock.None, _sut.Check());
    }

    [TestMethod]
    public void Check_TheHiddenCasesWinOverShortOfGold()
    {
        _adapter.PlayerGold.Returns(0);
        _adapter.IsPlayerDisguised.Returns(true);

        Assert.AreEqual(TidingsBlock.Disguised, _sut.Check());
    }

    [TestMethod]
    public void IsShown_IsTrueForNoneAndNotEnoughGoldOnly_ForEveryBlock()
    {
        foreach (var block in Enum.GetValues(typeof(TidingsBlock)).Cast<TidingsBlock>())
            Assert.AreEqual(block == TidingsBlock.None || block == TidingsBlock.NotEnoughGold, TidingsService.IsShown(block), block.ToString());
    }

    [TestMethod]
    public void TheFee_IsTwentyAndTheListThree()
    {
        Assert.AreEqual(20, TidingsService.Fee);
        Assert.AreEqual(3, TidingsService.MaxListed);
    }

    // --- Ask: filtering and order ---------------------------------------------------------------------------------

    [TestMethod]
    public void Ask_ListsTheNearestTowns_NearestFirst_AndChargesTheFee()
    {
        Sites(Site("c", 300f), Site("a", 100f), Site("b", 200f));

        var result = _sut.Ask();

        Assert.AreEqual(TidingsBlock.None, result.Block);
        CollectionAssert.AreEqual(new[] { "Town a", "Town b", "Town c" }, Towns(result));
        Assert.AreEqual("Realm a", result.Rows[0].Realm);
        _adapter.Received(1).ChargePlayer(TidingsService.Fee);
    }

    [TestMethod]
    public void Ask_TakesAtMostThree()
    {
        Sites(Site("a", 100f), Site("b", 200f), Site("c", 300f), Site("d", 400f), Site("e", 500f));

        CollectionAssert.AreEqual(new[] { "Town a", "Town b", "Town c" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_TheCurrentTown_IsNotListed()
    {
        Sites(Site("here", 0f, current: true), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_ATownAtWarWithThePlayer_IsNotListed()
    {
        Sites(Site("enemy", 50f, war: true), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_ANaNDistance_IsNotListed()
    {
        Sites(Site("nan", float.NaN), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_InfiniteAndMaxValueDistances_AreNotListed()
    {
        Sites(Site("inf", float.PositiveInfinity), Site("max", float.MaxValue), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_ANegativeDistance_IsNotListed()
    {
        Sites(Site("neg", -5f), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_AZeroDistance_IsNotListed_ItIsAFailedPathSearch()
    {
        Sites(Site("zero", 0f), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_ANaNCutoff_ListsNothing_AndChargesNothing()
    {
        _adapter.TournamentDistanceCutoff.Returns(float.NaN);
        Sites(Site("a", 100f));

        Assert.AreEqual(TidingsBlock.NoTournaments, _sut.Ask().Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Ask_ADistanceAtTheCutoff_IsNotListed_JustBelowIs()
    {
        Sites(Site("at", Cutoff), Site("below", Cutoff - 1f));

        CollectionAssert.AreEqual(new[] { "Town below" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_EqualDistances_BreakTheTieById()
    {
        Sites(Site("b", 100f), Site("c", 100f), Site("a", 100f));

        CollectionAssert.AreEqual(new[] { "Town a", "Town b", "Town c" }, Towns(_sut.Ask()));
    }

    [TestMethod]
    public void Ask_NoTournaments_SaysSo_AndChargesNothing()
    {
        Sites();

        var result = _sut.Ask();

        Assert.AreEqual(TidingsBlock.NoTournaments, result.Block);
        Assert.AreEqual(0, result.Rows.Count);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Ask_OnlyExcludedTowns_IsTheSameAsNone()
    {
        Sites(Site("enemy", 50f, war: true), Site("here", 0f, current: true), Site("nan", float.NaN));

        Assert.AreEqual(TidingsBlock.NoTournaments, _sut.Ask().Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    // --- Ask: the money -------------------------------------------------------------------------------------------

    [TestMethod]
    public void Ask_ShortOfGold_ChargesAndListsNothing()
    {
        _adapter.PlayerGold.Returns(19);
        Sites(Site("a", 100f));

        var result = _sut.Ask();

        Assert.AreEqual(TidingsBlock.NotEnoughGold, result.Block);
        Assert.AreEqual(0, result.Rows.Count);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Ask_ReReadsEveryGate_Disguised()
    {
        _adapter.IsPlayerDisguised.Returns(true);
        Sites(Site("a", 100f));

        Assert.AreEqual(TidingsBlock.Disguised, _sut.Ask().Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Ask_ReReadsEveryGate_SwitchOff()
    {
        _settings.EnableTidingsOfTheLists = false;
        Sites(Site("a", 100f));

        Assert.AreEqual(TidingsBlock.Disabled, _sut.Ask().Block);
        _adapter.DidNotReceive().ChargePlayer(Arg.Any<int>());
    }

    [TestMethod]
    public void Ask_AShortReceipt_ShowsNoList_AndRefundsWhatWasTaken()
    {
        Sites(Site("a", 100f));
        _adapter.ChargePlayer(TidingsService.Fee).Returns(5);

        var result = _sut.Ask();

        Assert.AreEqual(TidingsBlock.Problem, result.Block);
        Assert.AreEqual(0, result.Rows.Count);
        _adapter.Received(1).Refund(5);
    }

    [TestMethod]
    public void Ask_ReadsTheSurveyBeforeTheCharge_SoNothingIsTakenForAnEmptyAnswer()
    {
        Sites(Site("a", 100f));

        _sut.Ask();

        Received.InOrder(() =>
        {
            _adapter.ReadTournamentSites();
            _adapter.ChargePlayer(TidingsService.Fee);
        });
    }
}
