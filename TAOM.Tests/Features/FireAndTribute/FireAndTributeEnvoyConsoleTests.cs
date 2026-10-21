using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>The edges of the envoy's console command and status lines (#870 slice 2) that the main envoy tests do not reach.</summary>
[TestClass]
public class FireAndTributeEnvoyConsoleTests
{
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

    private string Text() => string.Join("\n", _r.Service.Describe());

    [TestMethod]
    public void BringEnvoyNow_AnUnreadableClock_IsRefused()
    {
        _r.Snapshots.Hours = double.NaN;

        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: false), "clock is unreadable");
    }

    [TestMethod]
    public void BringEnvoyNow_AHostGoingHome_SendsNoEnvoy()
    {
        _r.Edit(s => s.Hosts[0].Returning = true);

        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: false), "going home");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
    }

    [TestMethod]
    public void BringEnvoyNow_AnEnvoyAlreadyOverdue_KeepsItsEarlierDueHour()
    {
        _r.Edit(s => s.Hosts[0].EnvoyDueHour = 100);
        _r.Presenter.MapFree = false;

        _r.Service.BringEnvoyNow(dryRun: false);

        Assert.AreEqual(100, _r.LiveRecords().Single().EnvoyDueHour);
    }

    [TestMethod]
    public void BringEnvoyNow_AHostNeverScheduled_IsScheduledAndShownWhenThePlayerIsInTheTargetKingdom()
    {
        var rig = new FireAndTributeRig();
        rig.Random.DoubleFallback = 0.99;
        rig.OwnVillages(3);
        rig.Player.KingdomId = "vlandia";
        var host = rig.SpawnEvil();
        Assert.AreEqual(-1, host.EnvoyDueHour);
        rig.Player.KingdomId = "empire_w";

        rig.Service.BringEnvoyNow(dryRun: false);

        Assert.AreEqual(1, rig.Presenter.Offers.Count);
        Assert.AreEqual(2400, rig.LiveRecords().Single().EnvoyDueHour);
    }

    [TestMethod]
    public void BringEnvoyNow_ADryRunAfterAQuote_ShowsTheQuotedPriceNotANewOne()
    {
        _r.Snapshots.Hours = 2424;
        _r.Service.OnHourlyTick();
        _r.Presenter.Choose(EnvoyChoice.Delay);
        _r.OwnVillages(9);

        string text = _r.Service.BringEnvoyNow(dryRun: true);

        StringAssert.Contains(text, "3 villages, 1000 denars");
    }

    [TestMethod]
    public void Describe_AnEnvoyThatIsDueButWaiting_SaysSo()
    {
        _r.Presenter.MapFree = false;
        _r.Snapshots.Hours = 2430;
        _r.Service.OnHourlyTick();

        StringAssert.Contains(Text(), "envoy due at hour 2424 (due now, waiting)");
    }

    [TestMethod]
    public void Describe_AHagglingPlayersQuote_IsShown()
    {
        _r.OwnVillages(7);
        _r.Snapshots.Hours = 2424;
        _r.Service.OnHourlyTick();
        _r.Random.Doubles.Enqueue(0.1);
        _r.Presenter.Choose(EnvoyChoice.Haggle);
        _r.Presenter.Choose(EnvoyChoice.Delay);

        StringAssert.Contains(Text(), "quoted 1500 denars for 7 villages, haggled");
    }

    [TestMethod]
    public void Describe_AnUnreadableClock_DropsTheCountdownButKeepsTheHour()
    {
        _r.Snapshots.Hours = double.NaN;

        StringAssert.Contains(Text(), "envoy due at hour 2424.");
    }

    [TestMethod]
    public void Describe_AHostWithNoEnvoy_SaysSo()
    {
        var rig = new FireAndTributeRig();
        rig.Settings.EnvoysEnabled = false;
        rig.SpawnEvil();

        StringAssert.Contains(string.Join("\n", rig.Service.Describe()), "; no envoy.");
    }

    [TestMethod]
    public void Describe_ASettledEnvoyWithNoQuote_IsNoEnvoy()
    {
        _r.Edit(s => s.Hosts[0].EnvoyDone = true);

        StringAssert.Contains(Text(), "; no envoy.");
    }

    [TestMethod]
    public void Describe_NoGrudge_NeverPrintsTheWord()
    {
        Assert.IsFalse(Text().Contains("rudge"));
    }

    [TestMethod]
    public void TheEnvoyBlockers_AreReportedInTheOrderTheyAreChecked()
    {
        _r.Player.KingdomId = "vlandia";
        _r.Enlistment.IsEnlisted.Returns(true);
        _r.Presenter.MapFree = false;
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: true), "you belong to vlandia");

        _r.Player.KingdomId = "empire_w";
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: true), "enlisted");

        _r.Enlistment.IsEnlisted.Returns(false);
        StringAssert.Contains(_r.Service.BringEnvoyNow(dryRun: true), "map is not free");
    }

    [TestMethod]
    public void BringEnvoyNow_ADryRunWhileEnlisted_QuotesTheClansRealVillages()
    {
        _r.Enlistment.IsEnlisted.Returns(true);

        string text = _r.Service.BringEnvoyNow(dryRun: true);

        StringAssert.Contains(text, "enlisted");
        StringAssert.Contains(text, "3 villages, 1000 denars");
    }

    [TestMethod]
    public void BringEnvoyNow_ADryRunQuote_IsTheOneTheNextPopupShows()
    {
        _r.OwnVillages(7);
        _r.Edit(s => s.EnvoysSeized = 2);

        string dry = _r.Service.BringEnvoyNow(dryRun: true);
        _r.Service.BringEnvoyNow(dryRun: false);

        var offer = _r.Presenter.Last!;
        Assert.AreEqual(7, offer.Villages);
        Assert.AreEqual(6000, offer.Price);
        StringAssert.Contains(dry, $"{offer.Villages} villages, {offer.Price} denars");
    }

    [TestMethod]
    public void BringEnvoyNow_WhileTheHostIsInABattle_NamesIt_AndWaits()
    {
        _r.PartyOf(_host).Snapshot.InMapEvent = true;

        string text = _r.Service.BringEnvoyNow(dryRun: false);

        StringAssert.Contains(text, "battle");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.AreEqual(2400, _r.LiveRecords().Single().EnvoyDueHour, "due now, tried every hour");
    }

    [TestMethod]
    public void BringEnvoyNow_AHostWhoseSideIsSwitchedOff_IsLeftAlone()
    {
        _r.Settings.EvilHostsEnabled = false;

        string text = _r.Service.BringEnvoyNow(dryRun: false);

        StringAssert.Contains(text, "switched off");
        Assert.AreEqual(0, _r.Presenter.Offers.Count);
        Assert.AreEqual(2424, _r.LiveRecords().Single().EnvoyDueHour, "unchanged");
    }
}
