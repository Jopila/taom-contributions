using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.InnRounds.Domain;
using TAOM.Features.InnRounds.Hooks;
using TAOM.Features.NotableServices.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// The dialog behavior is a thin boundary over the service. It derives from the engine's campaign behavior base, so
/// constructing it needs the engine assemblies. The conditions that set a priced line's text variable are not run here: the
/// wiring tests pin the variables the lines read, and <see cref="InnRoundsTextsTests"/> renders the texts they carry.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class InnRoundsBehaviorTests
{
    private InnRoundsFixture _f = null!;
    private InnRoundsDialogBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new InnRoundsFixture();
        _behavior = new InnRoundsDialogBehavior(_f.Service, _f.Logger);
    }

    private InnRoundsDialogBehavior Another() => new(_f.Service, _f.Logger);

    /// <summary>The store a save wrote, now read as a load would (the engine hands a load the saved records).</summary>
    private static EngineDataStore Reload(EngineDataStore saved)
    {
        saved.Loading = true;
        return saved;
    }

    [TestMethod]
    public void ThePartyConsequence_StandsTheRound_AndCountsIt()
    {
        _behavior.Party();

        _f.Adapter.Received(1).PayTown(InnRoundsFixture.TownId, Arg.Any<int>());
        Assert.AreEqual(1, _behavior.Ledger.Count);
        Assert.AreEqual(10, _behavior.Ledger.Day);
    }

    [TestMethod]
    public void TheHouseConsequence_StandsTheRound_AndCountsIt()
    {
        _behavior.House();

        _f.Renown.Received(1).AddClanRenown(InnRoundsFixture.PlayerId, 2);
        Assert.AreEqual(1, _behavior.Ledger.Count);
    }

    [TestMethod]
    public void TheConsequences_ReCheckEveryGate()
    {
        _f.Settings.Enabled.Returns(false);

        _behavior.Party();
        _behavior.House();
        _behavior.Reveal();

        _f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
        Assert.AreEqual(0, _behavior.Ledger.Count);
        Assert.IsTrue(_behavior.Answered(RevealOutcome.Unavailable));
    }

    [TestMethod]
    public void TheRevealConsequence_StoresTheOutcomeForTheReplyLines()
    {
        _f.Keeper.AtWarWithPlayer = true;

        _behavior.Reveal();

        Assert.IsTrue(_behavior.Answered(RevealOutcome.AtWar));
        Assert.IsFalse(_behavior.Answered(RevealOutcome.Revealed));
    }

    [TestMethod]
    public void AFailedReveal_ClearsTheLastAnswer()
    {
        _f.HoldsAreNear();
        _behavior.Reveal();
        Assert.IsTrue(_behavior.Answered(RevealOutcome.Revealed));

        _f.Adapter.PlayerGold.Returns(0);
        _behavior.Reveal();

        Assert.IsFalse(_behavior.Answered(RevealOutcome.Revealed));
        Assert.IsTrue(_behavior.Answered(RevealOutcome.Unavailable));
    }

    [TestMethod]
    public void TheClickableConditions_GreyTheRoundsWhenShort_WithAReason()
    {
        _f.Adapter.PlayerGold.Returns(1);

        Assert.IsFalse(_behavior.PartyClickable(out var partyWhy));
        Assert.IsFalse(_behavior.HouseClickable(out var houseWhy));
        Assert.IsFalse(_behavior.RevealClickable(out var revealWhy));
        Assert.AreEqual("You don't have enough gold.", partyWhy.ToString());
        Assert.AreEqual("You don't have enough gold.", houseWhy.ToString());
        Assert.AreEqual("You don't have enough gold.", revealWhy.ToString());
    }

    [TestMethod]
    public void TheClickableConditions_AreOpenWhenThePlayerCanPay()
    {
        Assert.IsTrue(_behavior.PartyClickable(out _));
        Assert.IsTrue(_behavior.HouseClickable(out _));
        Assert.IsTrue(_behavior.RevealClickable(out _));
    }

    [TestMethod]
    public void AfterTheCap_BothRoundsAreGreyedWithTheCapReason_ButTheRevealIsNot()
    {
        for (var i = 0; i < 3; i++)
            _behavior.Party();

        Assert.IsFalse(_behavior.PartyClickable(out var partyWhy));
        Assert.IsFalse(_behavior.HouseClickable(out var houseWhy));
        Assert.IsTrue(_behavior.RevealClickable(out _));
        Assert.AreEqual(InnRoundsTexts.CapReached().ToString(), partyWhy.ToString());
        Assert.AreEqual(InnRoundsTexts.CapReached().ToString(), houseWhy.ToString());
        Assert.AreNotEqual("You don't have enough gold.", partyWhy.ToString());
    }

    [TestMethod]
    public void TheCapReason_WinsOverTheShortPurse()
    {
        for (var i = 0; i < 3; i++)
            _behavior.House();
        _f.Adapter.PlayerGold.Returns(0);

        _behavior.PartyClickable(out var why);

        Assert.AreEqual(InnRoundsTexts.CapReached().ToString(), why.ToString());
    }

    // ---- the saved day and count ----

    [TestMethod]
    public void SyncData_SaveThenLoad_RestoresTheDayAndTheCount()
    {
        _behavior.Party();
        _behavior.House();
        var store = new EngineDataStore { Loading = false };
        _behavior.SyncData(store);

        var loaded = Another();
        loaded.SyncData(Reload(store));

        Assert.AreEqual(2, loaded.Ledger.Count);
        Assert.AreEqual(10, loaded.Ledger.Day);
    }

    [TestMethod]
    public void SyncData_TheCapSurvivesALoad_OnTheSameDay()
    {
        for (var i = 0; i < 3; i++)
            _behavior.Party();
        var store = new EngineDataStore { Loading = false };
        _behavior.SyncData(store);

        var loaded = Another();
        loaded.SyncData(Reload(store));

        Assert.IsFalse(loaded.PartyClickable(out _), "the cap must not reset because the save was reloaded");
        loaded.Party();
        _f.Adapter.Received(3).PayTown(InnRoundsFixture.TownId, Arg.Any<int>());
    }

    [TestMethod]
    public void SyncData_ALoadEarlierOnTheSameDay_BringsBackThatSavesCount_NotTheLaterOne()
    {
        _behavior.Party();
        _behavior.Party();
        var saved = new EngineDataStore { Loading = false };
        _behavior.SyncData(saved);
        _behavior.House();
        Assert.IsFalse(_behavior.HouseClickable(out _));

        var loaded = Another();
        loaded.SyncData(Reload(saved));

        Assert.AreEqual(2, loaded.Ledger.Count);
        Assert.IsTrue(loaded.HouseClickable(out _), "one round is left that day");
    }

    [TestMethod]
    public void SyncData_ASavedDayThatIsNotToday_CountsAsNoRounds()
    {
        for (var i = 0; i < 3; i++)
            _behavior.Party();
        var store = new EngineDataStore { Loading = false };
        _behavior.SyncData(store);
        var loaded = Another();
        loaded.SyncData(Reload(store));

        _f.Adapter.Today.Returns(11);

        Assert.IsTrue(loaded.PartyClickable(out _));
    }

    [TestMethod]
    public void SyncData_ASaveWithoutTheKeys_LeavesAFreshLedger()
    {
        _behavior.SyncData(new EngineDataStore { Loading = true });

        Assert.AreEqual(0, _behavior.Ledger.Count);
        Assert.AreEqual(RoundLedger.NoDay, _behavior.Ledger.Day);
    }

    [TestMethod]
    public void EachCampaignsBehavior_HasItsOwnLedger()
    {
        _behavior.Party();

        Assert.AreEqual(0, Another().Ledger.Count);
    }
}
