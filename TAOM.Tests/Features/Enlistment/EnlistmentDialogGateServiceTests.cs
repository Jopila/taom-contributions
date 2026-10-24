using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentDialogGateServiceTests
{
    private EnlistmentStore _store = null!;
    private ICommanderLordAdapter _commander = null!;
    private IPlayerContextAdapter _playerContext = null!;
    private IEnlistmentConfigProvider _config = null!;
    private EnlistmentDialogGateService _gate = null!;
    private IServiceHistoryService _history = null!;
    private IPlayerPartyAdapter _playerParty = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentStore(Substitute.For<IModLogger>());
        _commander = Substitute.For<ICommanderLordAdapter>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();
        _config = Substitute.For<IEnlistmentConfigProvider>();
        _config.GetConfig().Returns(new EnlistmentCoreConfig());
        _history = Substitute.For<IServiceHistoryService>();
        _playerParty = Substitute.For<IPlayerPartyAdapter>();
        _logger = Substitute.For<IModLogger>();
        _gate = new EnlistmentDialogGateService(_store, _commander, _playerContext, _config, null, _history, _playerParty, _logger);

        _commander.IsLord("lord_1_1").Returns(true);
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true, name: "Lord Test"));
        _playerContext.IsUnderMercenaryService().Returns(false);
        _playerContext.GetPlayerKingdomId().Returns((string)null);
    }

    [TestMethod]
    public void CanEnlist_HealthyLordNoConflicts_Ok()
    {
        Assert.AreEqual(EnlistGateResult.Ok, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_AlreadyEnlisted_Rejected()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_9_9";

        Assert.AreEqual(EnlistGateResult.AlreadyEnlisted, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_NotALord_Rejected()
    {
        _commander.IsLord("townsman_1").Returns(false);

        Assert.AreEqual(EnlistGateResult.NotALord, _gate.CanEnlistWith("townsman_1"));
    }

    [TestMethod]
    public void CanEnlist_UnderMercenaryContract_Rejected()
    {
        _playerContext.IsUnderMercenaryService().Returns(true);

        Assert.AreEqual(EnlistGateResult.UnderMercenaryContract, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_CommanderUnfit_Rejected()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true, partyId: null));

        Assert.AreEqual(EnlistGateResult.CommanderUnavailable, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_CommanderAtWarWithPlayerKingdom_Rejected()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(true);

        Assert.AreEqual(EnlistGateResult.AtWarWithYourKingdom, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_PlayerKingdomAtPeaceWithCommander_Ok()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(false);

        Assert.AreEqual(EnlistGateResult.Ok, _gate.CanEnlistWith("lord_1_1"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CanEnlist_NullOrEmptyPartner_Rejected(string partnerId)
    {
        Assert.AreEqual(EnlistGateResult.NotALord, _gate.CanEnlistWith(partnerId));
    }

    [TestMethod]
    public void IsServingUnder_EnlistedWithThisCommander_True()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        Assert.IsTrue(_gate.IsServingUnder("lord_1_1"));
    }

    [TestMethod]
    public void IsServingUnder_DifferentLord_False()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        Assert.IsFalse(_gate.IsServingUnder("lord_2_2"));
    }

    [TestMethod]
    public void IsServingUnder_NotEnlisted_False()
    {
        Assert.IsFalse(_gate.IsServingUnder("lord_1_1"));
    }
    [TestMethod]
    public void EvaluateReleaseRequest_CommanderUnavailable_GrantsEvenMidContract()
    {
        // The term is WAIVED once the commander cannot hold up his end — not as a kindness, but
        // because the term cannot be SERVED. With no commander there are no duties to take, no
        // battles to join and no trust to earn, so refusing release leaves exactly one
        // interaction: stand still for N days or accept a desertion penalty for walking away from
        // a company that no longer exists.
        //
        // The contract below is deliberately mid-term: without the waiver this returns TooSoon.
        _store.Record.State = EnlistmentState.CommanderUnavailable;
        _store.Record.EnlistedAtDay = 100.0;

        var verdict = _gate.EvaluateReleaseRequest(101.0);

        Assert.AreEqual(ReleaseVerdict.Granted, verdict.Verdict,
            "losing your commander must not make you a deserter for leaving");
    }

    [TestMethod]
    public void CanEnlist_DesertedFromThatCommanderRecently_IsRefused()
    {
        _history.IsInDesertionCooldown("lord_1_1").Returns(true);

        Assert.AreEqual(EnlistGateResult.DesertedFromHim, _gate.CanEnlistWith("lord_1_1"));
    }

    // ---- the cost of his own men, stated in the oath question (#839) ----

    [TestMethod]
    public void OathRetinueCost_NoPaidMen_IsNothing_AndNeverAsksForTheWage()
    {
        _playerParty.GetMainPartyDailyWage().Returns(0);

        Assert.AreEqual((0, 0), _gate.OathRetinueCost("lord_1_1"));
        _history.DidNotReceiveWithAnyArgs().WageAtOath(default!, default!, default!);
    }

    [TestMethod]
    public void OathRetinueCost_PaidMen_IsTheirCostAndTheWageTheOathWouldStartAt()
    {
        _playerParty.GetMainPartyDailyWage().Returns(180);
        _playerParty.GetMainHeroId().Returns("main_hero");
        _history.WageAtOath("lord_1_1", "main_hero", _commander.GetSnapshot("lord_1_1")).Returns(14);

        Assert.AreEqual((180, 14), _gate.OathRetinueCost("lord_1_1"));
    }

    [TestMethod]
    public void OathRetinueCost_WhenAReadThrows_IsNothing_AndLogsOnceUntilItSucceeds()
    {
        var failing = true;
        _playerParty.GetMainPartyDailyWage().Returns(_ => failing ? throw new System.InvalidOperationException("boom") : 0);

        Assert.AreEqual((0, 0), _gate.OathRetinueCost("lord_1_1"));
        Assert.AreEqual((0, 0), _gate.OathRetinueCost("lord_1_1"));
        _logger.Received(1).LogError(Arg.Any<string>());

        failing = false;
        _gate.OathRetinueCost("lord_1_1");
        failing = true;
        _gate.OathRetinueCost("lord_1_1");
        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void CanEnlist_NoCooldown_StillOk()
    {
        _history.IsInDesertionCooldown("lord_1_1").Returns(false);

        Assert.AreEqual(EnlistGateResult.Ok, _gate.CanEnlistWith("lord_1_1"));
    }
}
