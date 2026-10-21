using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// The tribute envoy (#870 slice 2): when it is scheduled, when it may appear, what each option does, the grudge and the
/// spared clan. The rig's Gundabad host rides against empire_w on day 100 at campaign hour 2400, and the player's clan
/// (in empire_w) owns three villages: two price units, 1000 denars.
/// </summary>
[TestClass]
public class FireAndTributeEnvoyTests
{
    private const int StartHour = 2400;
    private const int DueHour = StartHour + 24;

    private FireAndTributeRig _r = null!;
    private RaidHostRecord _host = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
        _r.OwnVillages(3);
        _host = _r.SpawnEvil();
    }

    private RaidHostRecord Record() => _r.LiveRecords().Single();

    private void Tick(int hours = 1)
    {
        _r.Snapshots.Hours += hours;
        _r.Service.OnHourlyTick();
    }

    /// <summary>Moves the clock to the due hour and runs the hourly step that shows the envoy.</summary>
    private void Offer()
    {
        _r.Snapshots.Hours = DueHour - 1;
        Tick();
    }

    private EnvoyOffer Shown() => _r.Presenter.Last ?? throw new AssertFailedException("no envoy popup was shown");

    // ---- scheduling ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Spawn_AgainstThePlayersKingdom_SchedulesTheEnvoy_WithTheShortestWaitFirst()
    {
        Assert.AreEqual(DueHour, _host.EnvoyDueHour);
        Assert.IsFalse(_host.EnvoyDone);
    }

    [TestMethod]
    public void Spawn_TheWaitIsDrawnFromTheConfiguredRange()
    {
        var rig = new FireAndTributeRig();
        rig.Random.Ints.Enqueue(0); // the anchor
        rig.Random.Ints.Enqueue(0); // the quota extra
        rig.Random.Ints.Enqueue(24); // the envoy wait: 24 above the minimum is the maximum

        var host = rig.SpawnEvil();

        Assert.AreEqual(StartHour + 48, host.EnvoyDueHour);
    }

    [TestMethod]
    public void Spawn_UsesTheConfiguredWaitBounds()
    {
        var rig = new FireAndTributeRig();
        rig.ConfigProvider.Config.Tuning.EnvoyMinDelayHours = 30;
        rig.ConfigProvider.Config.Tuning.EnvoyMaxDelayHours = 30;

        Assert.AreEqual(StartHour + 30, rig.SpawnEvil().EnvoyDueHour);
    }

    [TestMethod]
    public void Spawn_AgainstAnotherKingdomThanThePlayers_SchedulesNothing()
    {
        var rig = new FireAndTributeRig();
        rig.Player.KingdomId = "vlandia";

        Assert.AreEqual(-1, rig.SpawnEvil().EnvoyDueHour);
    }

    [TestMethod]
    public void Spawn_ForAPlayerWithoutAKingdomOrWithoutAClanReading_SchedulesNothing()
    {
        var noKingdom = new FireAndTributeRig();
        noKingdom.Player.KingdomId = null;
        var noPlayer = new FireAndTributeRig();
        noPlayer.Player.Exists = false;

        Assert.AreEqual(-1, noKingdom.SpawnEvil().EnvoyDueHour);
        Assert.AreEqual(-1, noPlayer.SpawnEvil().EnvoyDueHour);
    }

    [TestMethod]
    public void Spawn_WithTheEnvoySwitchOff_SchedulesNothing()
    {
        var rig = new FireAndTributeRig();
        rig.Settings.EnvoysEnabled = false;

        Assert.AreEqual(-1, rig.SpawnEvil().EnvoyDueHour);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-5d)]
    [DataRow(3e10)]
    public void Spawn_WithAnUnreadableClock_SchedulesNothing_AndStillSpawns(double hours)
    {
        var rig = new FireAndTributeRig();
        rig.Snapshots.Hours = hours;

        var host = rig.SpawnEvil();

        Assert.AreEqual(-1, host.EnvoyDueHour);
    }

    // ---- when the popup comes --------------------------------------------------------------------------------------

    [TestMethod]
    public void Hour_BeforeTheDueHour_ShowsNothing()
    {
        _r.Snapshots.Hours = DueHour - 2;
        Tick();

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_AtTheDueHour_ShowsTheOfferWithThePriceAndTheOptionsOpen()
    {
        Offer();

        var offer = Shown();
        Assert.AreEqual(1, _r.Presenter.Offers.Count);
        Assert.AreEqual("{=taom_fat_host_gundabad}The Host of Gundabad", offer.KeyedHostName);
        Assert.AreEqual(3, offer.Villages);
        Assert.AreEqual(1000, offer.Price, "two units of 500");
        Assert.AreEqual(100000, offer.Gold);
        Assert.IsTrue(offer.CanAfford);
        Assert.IsFalse(offer.CanHaggle, "only from four units");
        Assert.AreEqual(7, offer.DelayDays);
    }

    [TestMethod]
    public void Hour_TheQuoteIsStoredOnTheRecord()
    {
        Offer();

        Assert.AreEqual(3, Record().EnvoyVillages);
        Assert.AreEqual(1000, Record().EnvoyPrice);
    }

    [TestMethod]
    public void Hour_FourUnits_OffersTheHaggle()
    {
        _r.OwnVillages(7);

        Offer();

        Assert.AreEqual(2000, Shown().Price);
        Assert.IsTrue(Shown().CanHaggle);
    }

    [TestMethod]
    public void Hour_EachSeizedEnvoy_AddsAFullPrice()
    {
        _r.Edit(s => s.EnvoysSeized = 2);

        Offer();

        Assert.AreEqual(3000, Shown().Price);
    }

    [TestMethod]
    public void Hour_UsesTheConfiguredUnitPrice()
    {
        _r.ConfigProvider.Config.Tuning.EnvoyUnitPrice = 80;

        Offer();

        Assert.AreEqual(160, Shown().Price);
    }

    [TestMethod]
    public void Hour_APoorPlayer_IsOfferedTheTributeAllTheSame_WithoutTheMeans()
    {
        _r.Player.Gold = 10;

        Offer();

        Assert.IsFalse(Shown().CanAfford);
    }

    [TestMethod]
    public void Hour_WhileTheMapIsNotFree_ShowsNothing_AndRetriesEveryHour()
    {
        _r.Presenter.MapFree = false;
        Offer();
        Tick();
        Tick(5);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.AreEqual(DueHour, Record().EnvoyDueHour, "still due");
        Assert.IsFalse(Record().EnvoyDone);

        _r.Presenter.MapFree = true;
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_AnEnlistedPlayer_IsNotAsked_UntilTheyLeaveTheService()
    {
        _r.Enlistment.IsEnlisted.Returns(true);
        Offer();
        Tick(3);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);

        _r.Enlistment.IsEnlisted.Returns(false);
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_ANoVillageClan_IsNotAsked_UntilItOwnsOne()
    {
        _r.DisownVillages();
        Offer();
        Tick(2);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.IsFalse(Record().EnvoyDone);

        _r.OwnVillages(1);
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
        Assert.AreEqual(1, Shown().Villages);
        Assert.AreEqual(500, Shown().Price, "one village is one unit");
    }

    [TestMethod]
    public void Hour_ANoVillageClansWaitingEnvoy_NeverReadsTheMapsVillageList()
    {
        _r.DisownVillages();
        Offer();
        _r.Adapter.Calls.Clear();

        Tick(5);

        Assert.AreEqual(0, _r.Adapter.Count("villages"), "the clan's count comes with the player reading");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_WhileTheHostIsInAMapEvent_NoEnvoyComes_UntilTheEventEnds()
    {
        _r.PartyOf(_host).Snapshot.InMapEvent = true;
        Offer();
        Tick(2);
        Assert.AreEqual(0, _r.Presenter.Offers.Count, "the host may be raiding one of the clan's villages");
        Assert.IsFalse(Record().EnvoyDone);

        _r.PartyOf(_host).Snapshot.InMapEvent = false;
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_AHostWhoseSideIsSwitchedOff_AndCannotBeRemovedYet_SendsNoEnvoy()
    {
        _r.Settings.EvilHostsEnabled = false;
        _r.Adapter.DespawnRefuses = true;

        Offer();
        Tick(2);

        Assert.AreEqual(1, _r.LiveRecords().Count, "still on the map, waiting to be removed");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_WithTheMasterSwitchOff_AHostThatCannotBeRemovedYet_SendsNoEnvoy()
    {
        _r.Settings.Enabled = false;
        _r.Adapter.DespawnRefuses = true;

        Offer();
        Tick(2);

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(-5d)]
    public void Hour_AnUnreadableClock_ShowsNothing_AndDoesNotThrow(double hours)
    {
        _r.Snapshots.Hours = hours;

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.AreEqual(DueHour, Record().EnvoyDueHour, "nothing is rescheduled");
    }

    [TestMethod]
    public void Hour_AHostSpawnedWhileThePlayerWasInAnotherKingdom_IsNeverAsked_EvenOnceThePlayerJoinsTheTarget()
    {
        var rig = new FireAndTributeRig();
        rig.Random.DoubleFallback = 0.99;
        rig.OwnVillages(3);
        rig.Player.KingdomId = "vlandia";
        rig.SpawnEvil();
        rig.Player.KingdomId = "empire_w";

        rig.Snapshots.Hours += 72;
        rig.Service.OnHourlyTick();
        rig.Snapshots.Hours += 1;
        rig.Service.OnHourlyTick();

        Assert.AreEqual(0, rig.Presenter.Offers.Count);
        Assert.AreEqual(-1, rig.LiveRecords().Single().EnvoyDueHour);
    }

    [TestMethod]
    public void Spawn_WhenReadingThePlayerThrows_LogsIt_AndTheHostStillRides()
    {
        var rig = new FireAndTributeRig();
        rig.Player.ReadThrows = true;

        var host = rig.SpawnEvil();

        Assert.AreEqual(-1, host.EnvoyDueHour);
        rig.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("scheduling the envoy")));
    }

    [TestMethod]
    public void Hour_APlayerOutsideTheTargetKingdom_IsNotAsked_UntilTheyAreBackInIt()
    {
        _r.Player.KingdomId = "vlandia";
        Offer();
        Tick(2);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);

        _r.Player.KingdomId = "empire_w";
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_ANoPlayerReading_ShowsNothingAndDoesNotThrow()
    {
        _r.Player.Exists = false;

        Offer();

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_AHostGoingHome_NeedsNoEnvoy_AndNeverGetsOne()
    {
        _r.Edit(s => s.Hosts[0].Returning = true);

        Offer();
        Tick(3);

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.IsTrue(Record().EnvoyDone);
    }

    [TestMethod]
    public void Hour_WithTheEnvoySwitchOff_ShowsNothing_UntilItIsBackOn()
    {
        _r.Settings.EnvoysEnabled = false;
        Offer();
        Tick(2);
        Assert.AreEqual(0, _r.Presenter.Offers.Count);

        _r.Settings.EnvoysEnabled = true;
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_InACoopSession_ShowsNoEnvoy()
    {
        _r.Coop.IsSessionActive.Returns(true);

        Offer();

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_WhileThePopupIsOpen_ShowsNoSecondOne()
    {
        Offer();
        Tick();
        Tick(10);

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Hour_APopupHiddenWithoutAnAnswer_ComesBackNextHour()
    {
        Offer();

        _r.Presenter.Hide();
        Tick();

        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.IsFalse(Record().EnvoyDone);
        Assert.AreEqual(0, _r.Player.Charged.Count);
    }

    [TestMethod]
    public void Hour_APopupThatThrows_IsLogged_AndRetriedNextHour()
    {
        _r.Presenter.ShowThrows = true;
        Offer();
        _r.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("envoy")));

        _r.Presenter.ShowThrows = false;
        Tick();

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Load_WhileThePopupIsOpen_ShowsItAgain()
    {
        Offer();
        _r.Presenter.Hide(); // loading a save closes every inquiry

        _r.Service.LoadFrom(_r.Service.CaptureState());
        Tick();

        Assert.AreEqual(2, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Load_OfTheSameSave_MakesTheOldPopupsClickHarmless()
    {
        Offer();
        var staleClick = _r.Presenter.OnChosen!;
        _r.Service.LoadFrom(_r.Service.CaptureState());

        staleClick(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count);
        Assert.AreEqual(string.Empty, Record().SparedClanId);
        Assert.IsFalse(Record().EnvoyDone);
    }

    [TestMethod]
    public void NewSession_WhileThePopupIsOpen_MakesTheOldPopupsClickHarmless()
    {
        Offer();

        _r.Service.ResetForNewSession();
        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count);
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void Hour_TwoDueEnvoys_ShowOneAtATime_TheOtherAfterTheFirstIsSettled()
    {
        _r.ConfigProvider.Config.Hosts.First(h => h.Id == "umbar").TargetSides = new[] { FactionSide.Free };
        _r.Service.TrySpawn(FactionSide.Neutral, dryRun: false);
        Assert.AreEqual(2, _r.LiveRecords().Count(r => r.EnvoyDueHour >= 0), "both host against the player's kingdom");

        Offer();
        Tick(5);
        Assert.AreEqual(1, _r.Presenter.Offers.Count, "one popup at a time");
        _r.Presenter.Choose(EnvoyChoice.Refuse);
        Tick();

        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual("{=taom_fat_host_umbar}The Corsair Host of Umbar", Shown().KeyedHostName);
    }

    // ---- the options -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Pay_ChargesThePrice_SparesTheClan_AndSettlesTheEnvoy()
    {
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        CollectionAssert.AreEqual(new[] { 1000 }, _r.Player.Charged);
        Assert.AreEqual(99000, _r.Player.Gold);
        Assert.AreEqual("player_clan", Record().SparedClanId);
        Assert.IsTrue(Record().EnvoyDone);
        Assert.AreEqual(EnvoyOutcome.Paid, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(1000, _r.Presenter.Outcomes.Single().Price);
        Assert.AreEqual(1, _r.Presenter.Offers.Count, "no second popup");
    }

    [TestMethod]
    public void Pay_WithTooLittleGold_TakesNothing_SaysSo_AndOffersAgain()
    {
        _r.Player.Gold = 999;
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count);
        Assert.AreEqual(string.Empty, Record().SparedClanId);
        Assert.IsFalse(Record().EnvoyDone);
        Assert.AreEqual(EnvoyOutcome.CannotAfford, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual(1000, Shown().Price);
    }

    [TestMethod]
    public void Pay_TheSecondClickOfOneOffer_ChargesOnce()
    {
        Offer();
        var choose = _r.Presenter.OnChosen!;

        choose(EnvoyChoice.Pay);
        choose(EnvoyChoice.Pay);

        Assert.AreEqual(1, _r.Player.Charged.Count);
    }

    [TestMethod]
    public void Pay_AfterTheHostIsGone_ChargesNothingAndDoesNotThrow()
    {
        Offer();
        _r.Service.OnPartyDestroyed(_host.PartyId);

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count);
        Assert.AreEqual(0, _r.Presenter.Outcomes.Count);
    }

    [TestMethod]
    public void Pay_WhenACoopSessionWentLiveMeanwhile_ChargesNothing()
    {
        Offer();
        _r.Coop.IsSessionActive.Returns(true);

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count);
    }

    [TestMethod]
    public void Pay_WithNoPlayerClan_TakesNothing_AndOffersAgain()
    {
        Offer();
        _r.Player.ClanId = null;

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(0, _r.Player.Charged.Count, "gold taken without a clan to spare would be a theft");
        Assert.AreEqual(string.Empty, Record().SparedClanId);
        Assert.IsFalse(Record().EnvoyDone);
        Assert.AreEqual(EnvoyOutcome.CannotAfford, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Pay_AnOutcomeLineThatThrows_IsLoggedNotRaised_AndThePaymentStands()
    {
        Offer();
        _r.Presenter.OutcomeThrows = true;

        _r.Presenter.Choose(EnvoyChoice.Pay);

        _r.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("envoy choice")));
        Assert.AreEqual("player_clan", Record().SparedClanId);
        CollectionAssert.AreEqual(new[] { 1000 }, _r.Player.Charged);
    }

    [TestMethod]
    public void Pay_APaymentThatFailsAfterTheGoldCheck_IsToldAsCannotAfford()
    {
        Offer();
        _r.Player.Gold = 0;

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual(EnvoyOutcome.CannotAfford, _r.Presenter.Outcomes.Single().Outcome);
    }

    [TestMethod]
    public void Haggle_AGoodRoll_CutsThePriceToThreeQuartersRoundedUp_AndOffersAgain()
    {
        _r.OwnVillages(7);
        Offer(); // 2000
        _r.Random.Doubles.Enqueue(0.1);

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(1500, Record().EnvoyPrice);
        Assert.IsTrue(Record().EnvoyHaggled);
        Assert.AreEqual(EnvoyOutcome.HaggleWon, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(1500, _r.Presenter.Outcomes.Single().Price);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual(1500, Shown().Price);
        Assert.IsFalse(Shown().CanHaggle, "once");
    }

    [TestMethod]
    public void Haggle_ABadRoll_KeepsThePrice_AndOffersAgainWithoutTheHaggle()
    {
        _r.OwnVillages(7);
        Offer();
        _r.Random.Doubles.Enqueue(0.25); // the chance at Charm 0 is 0.20

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(2000, Record().EnvoyPrice);
        Assert.IsTrue(Record().EnvoyHaggled);
        Assert.AreEqual(EnvoyOutcome.HaggleLost, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual(2000, Shown().Price);
        Assert.IsFalse(Shown().CanHaggle);
    }

    [TestMethod]
    public void Haggle_CharmRaisesTheChance_AndTheRollMustBeBelowIt()
    {
        _r.OwnVillages(7);
        _r.Player.Charm = 250; // 0.20 + 250 x 0.002 = 0.70, the cap
        Offer();
        _r.Random.Doubles.Enqueue(0.6);

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(EnvoyOutcome.HaggleWon, _r.Presenter.Outcomes.Single().Outcome);
    }

    [TestMethod]
    public void Haggle_TheSameRollFailsWithoutCharm()
    {
        _r.OwnVillages(7);
        Offer();
        _r.Random.Doubles.Enqueue(0.6);

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(EnvoyOutcome.HaggleLost, _r.Presenter.Outcomes.Single().Outcome);
    }

    [TestMethod]
    public void Haggle_BelowFourUnits_IsRefused_WithoutARoll_AndTheOfferComesBack()
    {
        Offer();
        int rolls = _r.Random.DoubleCalls;

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(rolls, _r.Random.DoubleCalls);
        Assert.IsFalse(Record().EnvoyHaggled);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Haggle_OnlyOnce_ASecondClickIsRefusedWithoutARoll()
    {
        _r.OwnVillages(7);
        Offer();
        var choose = _r.Presenter.OnChosen!;
        _r.Random.Doubles.Enqueue(0.99);
        choose(EnvoyChoice.Haggle);
        int rolls = _r.Random.DoubleCalls;

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(rolls, _r.Random.DoubleCalls);
        Assert.AreEqual(1, _r.Presenter.Outcomes.Count);
    }

    [TestMethod]
    public void Haggle_ThenPay_ChargesTheCutPrice()
    {
        _r.OwnVillages(7);
        Offer();
        _r.Random.Doubles.Enqueue(0.1);
        _r.Presenter.Choose(EnvoyChoice.Haggle);

        _r.Presenter.Choose(EnvoyChoice.Pay);

        CollectionAssert.AreEqual(new[] { 1500 }, _r.Player.Charged);
    }

    [TestMethod]
    public void Haggle_UsesTheConfiguredChance()
    {
        _r.OwnVillages(7);
        _r.ConfigProvider.Config.Tuning.HaggleBaseChance = 0.9f;
        _r.ConfigProvider.Config.Tuning.HaggleMaxChance = 0.95f;
        Offer();
        _r.Random.Doubles.Enqueue(0.8);

        _r.Presenter.Choose(EnvoyChoice.Haggle);

        Assert.AreEqual(EnvoyOutcome.HaggleWon, _r.Presenter.Outcomes.Single().Outcome);
    }

    [TestMethod]
    public void Delay_ReturnsInTheConfiguredDays_AtTheSamePrice_WithNothingSparedMeanwhile()
    {
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Delay);

        Assert.AreEqual(DueHour + 168, Record().EnvoyDueHour);
        Assert.IsFalse(Record().EnvoyDone);
        Assert.AreEqual(string.Empty, Record().SparedClanId);
        Assert.AreEqual(1000, Record().EnvoyPrice);
        Assert.AreEqual(EnvoyOutcome.Delayed, _r.Presenter.Outcomes.Single().Outcome);
        Assert.AreEqual(7, _r.Presenter.Outcomes.Single().Days);
        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void Delay_TheEnvoyReturnsAfterTheWeek_AtTheQuotedPrice_EvenIfTheClanGrew()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Delay);
        _r.OwnVillages(9);

        Tick(167);
        Assert.AreEqual(1, _r.Presenter.Offers.Count, "a week is 168 hours");
        Tick();

        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual(1000, Shown().Price, "the price stays");
        Assert.AreEqual(3, Shown().Villages);
    }

    [TestMethod]
    public void Delay_UsesTheConfiguredDays()
    {
        _r.ConfigProvider.Config.Tuning.EnvoyDelayDays = 3;
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Delay);

        Assert.AreEqual(DueHour + 72, Record().EnvoyDueHour);
        Assert.AreEqual(3, _r.Presenter.Outcomes.Single().Days);
    }

    [TestMethod]
    public void Delay_AHagglingPlayerKeepsTheCutPriceAndTheSpentHaggle()
    {
        _r.OwnVillages(7);
        Offer();
        _r.Random.Doubles.Enqueue(0.1);
        _r.Presenter.Choose(EnvoyChoice.Haggle);
        _r.Presenter.Choose(EnvoyChoice.Delay);

        Tick(168);

        Assert.AreEqual(1500, Shown().Price);
        Assert.IsFalse(Shown().CanHaggle);
    }

    [TestMethod]
    public void Refuse_SettlesTheEnvoy_SparesNobody_AndSeizesNothing()
    {
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Refuse);

        Assert.IsTrue(Record().EnvoyDone);
        Assert.AreEqual(string.Empty, Record().SparedClanId);
        Assert.AreEqual(0, _r.State().EnvoysSeized);
        Assert.AreEqual(0, _r.State().Grudges.Count);
        Assert.AreEqual(0, _r.Player.Charged.Count);
        Assert.AreEqual(EnvoyOutcome.Refused, _r.Presenter.Outcomes.Single().Outcome);
        Tick(500);
        Assert.AreEqual(1, _r.Presenter.Offers.Count, "one envoy per host");
    }

    [TestMethod]
    public void Seize_SettlesTheEnvoy_CountsIt_AndMarksTheSidesNextHostForTheKingdom()
    {
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Seize);

        var state = _r.State();
        Assert.IsTrue(state.Hosts.Single().EnvoyDone);
        Assert.AreEqual(1, state.EnvoysSeized);
        Assert.AreEqual("empire_w", state.Grudges[FactionSide.Evil]);
        Assert.AreEqual(1, state.Grudges.Count);
        Assert.AreEqual(string.Empty, state.Hosts.Single().SparedClanId);
        Assert.AreEqual(EnvoyOutcome.Seized, _r.Presenter.Outcomes.Single().Outcome);
    }

    [TestMethod]
    public void Seize_ThePriceOfTheNextTributeRises()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Seize);
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Snapshots.Day = 400;
        var second = _r.SpawnEvil();
        _r.Snapshots.Hours = second.EnvoyDueHour;
        _r.Service.OnHourlyTick();

        Assert.AreEqual(2, _r.Presenter.Offers.Count);
        Assert.AreEqual(2000, Shown().Price, "one full price more");
    }

    [TestMethod]
    public void Seize_TheCountStopsAtTheSaveFormatsLimit()
    {
        _r.Edit(s => s.EnvoysSeized = FireAndTributeSaveCodec.MaxSeized);
        Offer();

        _r.Presenter.Choose(EnvoyChoice.Seize);

        Assert.AreEqual(FireAndTributeSaveCodec.MaxSeized, _r.State().EnvoysSeized);
    }

    [TestMethod]
    public void AnUnknownChoice_IsIgnoredAndOffersAgain()
    {
        Offer();

        _r.Presenter.Choose((EnvoyChoice)99);

        Assert.IsFalse(Record().EnvoyDone);
        Assert.AreEqual(2, _r.Presenter.Offers.Count);
    }

    // ---- the grudge ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Grudge_TheSidesNextHostRidesAgainstThatKingdomFirst_NotTheStrongest()
    {
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Edit(s => s.Grudges[FactionSide.Evil] = "vlandia");

        var next = _r.SpawnEvil();

        Assert.AreEqual("vlandia", next.TargetKingdomId, "empire_w is stronger");
    }

    [TestMethod]
    public void Grudge_IsSpentAtSpawn_SoTheHostAfterGoesForTheStrongestAgain()
    {
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Edit(s => s.Grudges[FactionSide.Evil] = "vlandia");
        var first = _r.SpawnEvil();
        Assert.AreEqual(0, _r.State().Grudges.Count, "spent");
        _r.Service.OnPartyDestroyed(first.PartyId);

        Assert.AreEqual("empire_w", _r.SpawnEvil().TargetKingdomId);
    }

    [TestMethod]
    public void Grudge_BeatsAPreferredTarget()
    {
        _r.ConfigProvider.Config.Hosts.First(h => h.Id == "gundabad").PreferredTargets = new[] { "empire_w" };
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Edit(s => s.Grudges[FactionSide.Evil] = "vlandia");

        Assert.AreEqual("vlandia", _r.SpawnEvil().TargetKingdomId);
    }

    [TestMethod]
    public void Grudge_AgainstAKingdomWithNoRaidableVillage_IsSpentAnyway_AndTheHostRidesAgainstTheStrongest()
    {
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Adapter.RemoveVillagesOf("vlandia");
        _r.Edit(s => s.Grudges[FactionSide.Evil] = "vlandia");

        var next = _r.SpawnEvil();

        Assert.AreEqual("empire_w", next.TargetKingdomId);
        Assert.AreEqual(0, _r.State().Grudges.Count);
    }

    [TestMethod]
    public void Grudge_IsHeldPerSide_AHostOfAnotherSideDoesNotSpendIt()
    {
        _r.Edit(s => s.Grudges[FactionSide.Neutral] = "vlandia");
        _r.Service.OnPartyDestroyed(_host.PartyId);

        _r.SpawnEvil();

        Assert.AreEqual("vlandia", _r.State().Grudges[FactionSide.Neutral]);
    }

    [TestMethod]
    public void Grudge_ADryRunUsesItWithoutSpendingIt()
    {
        _r.Service.OnPartyDestroyed(_host.PartyId);
        _r.Edit(s => s.Grudges[FactionSide.Evil] = "vlandia");

        string text = _r.Service.TrySpawn(FactionSide.Evil, dryRun: true);

        StringAssert.Contains(text, "against vlandia");
        Assert.AreEqual("vlandia", _r.State().Grudges[FactionSide.Evil]);
    }

    [TestMethod]
    public void Grudge_SurvivesASaveAndALoad()
    {
        _r.Presenter.MapFree = true;
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Seize);
        var fresh = new FireAndTributeRig();

        fresh.Service.LoadFrom(_r.Service.CaptureState());

        Assert.AreEqual("empire_w", fresh.State().Grudges[FactionSide.Evil]);
        Assert.AreEqual(1, fresh.State().EnvoysSeized);
    }

    // ---- the spared clan -------------------------------------------------------------------------------------------

    private void OwnTheNearestVillage() =>
        _r.Adapter.Put(new VillageReading(true, "empire_w", true, 400f, "empire_w_v1", 101f, 100f, "player_clan"));

    private IEnumerable<string> RaidOrders() => _r.Adapter.Calls.Where(c => c.StartsWith("raid:", StringComparison.Ordinal));

    [TestMethod]
    public void APaidHost_NeverOrdersARaidOnAVillageOfTheSparedClan_EvenTheNearest()
    {
        OwnTheNearestVillage();
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);
        _r.Adapter.Parties[_host.PartyId].Snapshot.RaidTargetId = null;
        _r.Adapter.Calls.Clear();

        Tick();

        CollectionAssert.AreEqual(new[] { $"raid:{_host.PartyId}:empire_w_v2" }, RaidOrders().ToArray());
    }

    [TestMethod]
    public void AnUnpaidHost_StillRaidsTheClansNearestVillage()
    {
        OwnTheNearestVillage();
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Refuse);
        _r.Adapter.Parties[_host.PartyId].Snapshot.RaidTargetId = null;
        _r.Adapter.Calls.Clear();

        Tick();

        CollectionAssert.AreEqual(new[] { $"raid:{_host.PartyId}:empire_w_v1" }, RaidOrders().ToArray());
    }

    [TestMethod]
    public void APaidHost_DropsARaidOrderOnTheSparedClansVillage_AtPayment()
    {
        OwnTheNearestVillage();
        Offer();
        _r.Adapter.Parties[_host.PartyId].Snapshot.RaidTargetId = "empire_w_v1";
        _r.Adapter.Calls.Clear();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        CollectionAssert.AreEqual(new[] { $"raid:{_host.PartyId}:empire_w_v2" }, RaidOrders().ToArray());
    }

    [TestMethod]
    public void APaidHost_StillKeepsAValidOrderOnAnotherClansVillage()
    {
        Offer();
        _r.Adapter.Parties[_host.PartyId].Snapshot.RaidTargetId = "empire_w_v1";
        _r.Presenter.Choose(EnvoyChoice.Pay);
        _r.Adapter.Calls.Clear();

        Tick();

        Assert.AreEqual(0, RaidOrders().Count(), "the standing order is still good");
    }

    [TestMethod]
    public void APaidHost_NeverEngagesThePlayersParty_ButStillEngagesAnyOtherParty()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);
        _r.Adapter.Nearby.Add(new NearbyParty { Id = "main", KingdomId = "empire_w", Strength = 50f, Speed = 3f, Distance = 1f, IsMainParty = true });
        _r.Adapter.Nearby.Add(new NearbyParty { Id = "companion", KingdomId = "empire_w", Strength = 50f, Speed = 3f, Distance = 4f });
        _r.Adapter.Calls.Clear();

        Tick();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:companion"));
        Assert.IsFalse(_r.Adapter.Called($"engage:{_host.PartyId}:main"));
    }

    [TestMethod]
    public void AnUnpaidHost_EngagesThePlayersParty()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Refuse);
        _r.Adapter.Nearby.Add(new NearbyParty { Id = "main", KingdomId = "empire_w", Strength = 50f, Speed = 3f, Distance = 1f, IsMainParty = true });
        _r.Adapter.Calls.Clear();

        Tick();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:main"));
    }

    [TestMethod]
    public void TheSparedClan_SurvivesASaveAndALoad()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);
        var fresh = new FireAndTributeRig();
        fresh.Adapter.Parties[_host.PartyId] = new FatParty();

        fresh.Service.LoadFrom(_r.Service.CaptureState());

        Assert.AreEqual("player_clan", fresh.LiveRecords().Single().SparedClanId);
        Assert.IsTrue(fresh.LiveRecords().Single().EnvoyDone);
    }

    [TestMethod]
    public void APaymentSparesOnlyThatHost()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);

        _r.Service.TrySpawn(FactionSide.Neutral, dryRun: false);

        Assert.AreEqual(string.Empty, _r.LiveRecords().Single(r => r.Side == FactionSide.Neutral).SparedClanId);
    }

    // ---- the console's "bring the envoy now" -----------------------------------------------------------------------

    [TestMethod]
    public void BringEnvoyNow_NoHost_SaysSo()
    {
        var rig = new FireAndTributeRig();

        StringAssert.Contains(rig.Service.BringEnvoyNow(dryRun: false), "No host is out");
    }

    [TestMethod]
    public void BringEnvoyNow_ADryRun_ReportsTheQuoteAndChangesNothing()
    {
        string before = _r.Service.CaptureState();

        string text = _r.Service.BringEnvoyNow(dryRun: true);

        StringAssert.Contains(text, "gundabad");
        StringAssert.Contains(text, "3 villages");
        StringAssert.Contains(text, "1000 denars");
        Assert.AreEqual(before, _r.Service.CaptureState());
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void BringEnvoyNow_Confirmed_ShowsTheOfferAtOnce_WithoutWaitingForTheDueHour()
    {
        string text = _r.Service.BringEnvoyNow(dryRun: false);

        Assert.AreEqual(1, _r.Presenter.Offers.Count);
        Assert.AreEqual(StartHour, Record().EnvoyDueHour);
        StringAssert.Contains(text, "shown");
    }

    [TestMethod]
    public void BringEnvoyNow_WhileTheMapIsBusy_SetsItDue_AndSaysItRetriesEveryHour()
    {
        _r.Presenter.MapFree = false;

        string text = _r.Service.BringEnvoyNow(dryRun: false);

        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.AreEqual(StartHour, Record().EnvoyDueHour);
        StringAssert.Contains(text, "map is not free");
        StringAssert.Contains(text, "every hour");
    }

    [TestMethod]
    public void BringEnvoyNow_AHostAgainstAnotherKingdom_IsRefused()
    {
        _r.Player.KingdomId = "vlandia";

        string text = _r.Service.BringEnvoyNow(dryRun: false);

        StringAssert.Contains(text, "rides against empire_w");
        StringAssert.Contains(text, "vlandia");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void BringEnvoyNow_ASettledEnvoy_IsNotBroughtAgain()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Refuse);

        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: false), "already settled");
    }

    [TestMethod]
    public void BringEnvoyNow_AnOpenPopup_IsNotDoubled()
    {
        Offer();

        string text = _r.Service.BringEnvoyNow(dryRun: false);

        StringAssert.Contains(text, "map is not free");
        Assert.AreEqual(1, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void BringEnvoyNow_SwitchOffOrNotSinglePlayer_IsRefused()
    {
        _r.Settings.EnvoysEnabled = false;
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: false), "switched off");
        _r.Settings.EnvoysEnabled = true;
        _r.Coop.IsSessionActive.Returns(true);

        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: false), "single-player only");
    }

    [TestMethod]
    public void BringEnvoyNow_NamesWhatBlocksItNow_InADryRun()
    {
        _r.Enlistment.IsEnlisted.Returns(true);
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: true), "enlisted");

        _r.Enlistment.IsEnlisted.Returns(false);
        _r.DisownVillages();
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: true), "no village");
    }

    // ---- the status lines ------------------------------------------------------------------------------------------

    [TestMethod]
    public void Describe_ShowsTheEnvoySwitchTheSeizedCountAndEachHostsEnvoy()
    {
        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, "Tribute envoys: on, 0 seized");
        StringAssert.Contains(text, $"envoy due at hour {DueHour} (in 24 h)");
    }

    [TestMethod]
    public void Describe_AQuotedPaidAndSeizedState_IsShown()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);
        _r.Edit(s => { s.EnvoysSeized = 2; s.Grudges[FactionSide.Free] = "mordor"; });

        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, "envoy settled (1000 denars for 3 villages), spares player_clan");
        StringAssert.Contains(text, "Tribute envoys: on, 2 seized");
        StringAssert.Contains(text, "Grudges: Free -> mordor");
    }

    [TestMethod]
    public void Describe_OffSwitch_IsShown()
    {
        _r.Settings.EnvoysEnabled = false;

        StringAssert.Contains(string.Join("\n", _r.Service.Describe()), "Tribute envoys: OFF in MCM");
    }
}
