using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>The console's spawn, the status lines, the save record through the service, and the session reset.</summary>
[TestClass]
public class FireAndTributeServiceLifecycleTests
{
    private FireAndTributeRig _r = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
    }

    // ---- TrySpawn --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TrySpawn_IgnoresTheTimersTheChanceAndTheCooldown()
    {
        _r.WarOfTheRing.CurrentPhase.Returns(WarPhase.Peace); // before Full War, with the roll at 0.99
        _r.Service.LoadFrom("{\"v\":1,\"seen\":-1,\"cool\":{\"Evil\":9999},\"hosts\":[]}");

        string result = _r.Service.TrySpawn(FactionSide.Evil, dryRun: false);

        StringAssert.Contains(result, "gundabad rides against empire_w");
        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Random.DoubleCalls);
    }

    [TestMethod]
    public void TrySpawn_DryRun_ReportsThePlanAndChangesNothing()
    {
        string result = _r.Service.TrySpawn(FactionSide.Evil, dryRun: true);

        StringAssert.Contains(result, "Would send gundabad (gundabad_raiders)");
        StringAssert.Contains(result, "against empire_w");
        StringAssert.Contains(result, "102 men");
        StringAssert.Contains(result, "quota 6 to 9");
        Assert.AreEqual(0, _r.Adapter.Count("spawn:"));
        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);
    }

    [TestMethod]
    public void TrySpawn_DryRun_StillReportsASkippedHostAndItsReason()
    {
        _r.Adapter.ProbeByClan["gundabad_raiders"] = RaidSpawnFailure.NoHideout;

        string result = _r.Service.TrySpawn(FactionSide.Evil, dryRun: true);

        StringAssert.Contains(result, "NoHideout");
        Assert.AreEqual(0, _r.Adapter.Parties.Count);
    }

    [TestMethod]
    public void TrySpawn_ASideThatAlreadyHasAHost_IsRefused()
    {
        _r.Service.TrySpawn(FactionSide.Evil, dryRun: false);

        string result = _r.Service.TrySpawn(FactionSide.Evil, dryRun: false);

        StringAssert.StartsWith(result, "Refused: Evil already has a host");
        Assert.AreEqual(1, _r.Adapter.Count("spawn:"));
    }

    [TestMethod]
    public void TrySpawn_ASideSwitchedOff_IsRefused()
    {
        _r.Settings.EvilHostsEnabled = false;

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Evil, dryRun: false), "switched off");
        Assert.AreEqual(0, _r.Adapter.Count("spawn:"));
    }

    [TestMethod]
    public void TrySpawn_TheMasterSwitchOff_IsRefused()
    {
        _r.Settings.Enabled = false;

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Free, dryRun: false), "switched off");
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void TrySpawn_InCoopOrOnADedicatedServer_IsRefused_DryRunIncluded(bool session, bool defer, bool dedicated)
    {
        _r.Coop.IsSessionActive.Returns(session);
        _r.Coop.ShouldDeferToHost.Returns(defer);
        _r.Dedicated.IsDedicatedServer.Returns(dedicated);

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Evil, dryRun: true), "single-player only");
        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Evil, dryRun: false), "single-player only");
        Assert.AreEqual(0, _r.Adapter.Calls.Count);
    }

    [TestMethod]
    public void TrySpawn_AnUnreadableDay_IsRefused()
    {
        _r.Snapshots.DayReadable = false;

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Evil, dryRun: false), "clock is unreadable");
    }

    [TestMethod]
    public void TrySpawn_ASideWithNoConfiguredHost_SaysSo()
    {
        _r.ConfigProvider.Config = new FireAndTributeConfig();

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Free, dryRun: false), "no Free host is configured");
    }

    [TestMethod]
    public void TrySpawn_ASideWithNothingToRaid_SaysSo()
    {
        _r.Adapter.Villages.Clear();

        StringAssert.Contains(_r.Service.TrySpawn(FactionSide.Evil, dryRun: false), "no kingdom of Free has a raidable village");
    }

    // ---- Describe --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Describe_NoHost_ShowsEachSideAtRest()
    {
        var lines = _r.Service.Describe();

        StringAssert.Contains(lines[0], "Fire and Tribute: on, single-player, day 100, 3 host(s) configured");
        Assert.IsTrue(lines.Any(l => l.StartsWith("Free (on): no host, no cooldown")));
        Assert.IsTrue(lines.Any(l => l.StartsWith("Neutral (on): no host")));
        Assert.IsTrue(lines.Any(l => l.StartsWith("Evil (on): no host")));
    }

    [TestMethod]
    public void Describe_ALiveHost_ShowsItsPartyTargetAndProgress()
    {
        var host = _r.SpawnEvil();
        _r.Service.OnVillageStateChanged(true, host.PartyId);

        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, $"Evil (on): gundabad party {host.PartyId} against empire_w, raids 1/6, since day 100, raiding");
        StringAssert.Contains(text, "Today: spawned 1, raids 1");
    }

    [TestMethod]
    public void Describe_SaysWhenTheSwitchesAreOffOrTheGameIsNotSinglePlayer()
    {
        _r.Settings.Enabled = false;
        _r.Coop.IsSessionActive.Returns(true);

        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, "OFF in MCM");
        StringAssert.Contains(text, "NOT single-player");
        StringAssert.Contains(text, "Evil (off)");
    }

    [TestMethod]
    public void Describe_ShowsACooldown_AndTheFullWarDay()
    {
        _r.Service.LoadFrom("{\"v\":1,\"seen\":44,\"cool\":{\"Free\":300},\"hosts\":[]}");

        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, "Free (on): no host, cooldown until day 300.");
        Assert.IsFalse(text.Contains("grudge"));
        StringAssert.Contains(text, "Full War first seen day 44");
    }

    [TestMethod]
    public void Describe_TheWindowIsTheOneTheNextRollWouldUse_NotTheOneBeforeTheWarIsNoted()
    {
        // Full War is on at day 100 but not yet noted: the daily roll notes it first, so the window then opens on day 145.
        string before = _r.Service.CaptureState();

        var text = string.Join("\n", _r.Service.Describe());

        StringAssert.Contains(text, "Spawn window: closed");
        StringAssert.Contains(text, "Full War first seen day 100");
        Assert.AreEqual(before, _r.Service.CaptureState(), "describing mutates nothing");

        _r.Snapshots.Day = 100;
        _r.Service.OnDailyTick();
        _r.Snapshots.Day = 144;
        StringAssert.Contains(string.Join("\n", _r.Service.Describe()), "Spawn window: closed");
        _r.Snapshots.Day = 145;
        StringAssert.Contains(string.Join("\n", _r.Service.Describe()), "Spawn window: open");
    }

    [TestMethod]
    public void Describe_AnUnreadableDay_DoesNotThrow()
    {
        _r.Snapshots.DayReadable = false;

        StringAssert.Contains(_r.Service.Describe()[0], "day unreadable");
    }

    // ---- save record -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void CaptureThenLoad_IntoAFreshService_RestoresEveryField()
    {
        var host = _r.SpawnEvil();
        _r.Service.OnVillageStateChanged(true, host.PartyId);
        string json = _r.Service.CaptureState();
        var fresh = new FireAndTributeRig();

        fresh.Service.LoadFrom(json);

        Assert.AreEqual(json, fresh.Service.CaptureState());
        Assert.AreEqual(1, fresh.LiveRecords().Single().Raids);
    }

    [TestMethod]
    public void LoadFrom_Garbage_LogsWarningsAndLeavesTheServiceReset()
    {
        _r.SpawnEvil();

        _r.Service.LoadFrom("{ not json");

        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(1, _r.Warnings("save record"));
    }

    [TestMethod]
    public void LoadFrom_Null_LeavesTheServiceReset_WithoutAWarning()
    {
        _r.SpawnEvil();

        _r.Service.LoadFrom(null);

        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Warnings("save record"));
    }

    [TestMethod]
    public void LoadFrom_ASkippedRow_IsWarnedAboutAndTheRestKept()
    {
        _r.Service.LoadFrom("{\"v\":1,\"hosts\":[{\"p\":\"\"},{\"p\":\"p1\",\"h\":\"umbar\",\"s\":\"Neutral\",\"t\":\"empire_w\",\"d\":5,\"r\":0,\"q\":6}]}");

        Assert.AreEqual("p1", _r.LiveRecords().Single().PartyId);
        Assert.AreEqual(1, _r.Warnings("host row 0 skipped"));
    }

    [TestMethod]
    public void LoadFrom_ARestoredHost_KeepsRaidingAfterTheLoad()
    {
        var host = _r.SpawnEvil();
        string json = _r.Service.CaptureState();
        var fresh = new FireAndTributeRig();
        fresh.Adapter.Parties[host.PartyId] = new FatParty();
        fresh.Service.LoadFrom(json);
        fresh.Service.OnSessionLaunched();
        fresh.Adapter.Calls.Clear();

        fresh.Snapshots.Day = 101;
        fresh.Service.OnHourlyTick();

        Assert.IsTrue(fresh.Adapter.Calls.Any(c => c.StartsWith($"raid:{host.PartyId}:empire_w_v")));
    }

    // ---- session reset ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void ResetForNewSession_ClearsHostsCooldownsTheSeenDayAndTheCounters()
    {
        _r.Snapshots.Day = 44;
        _r.Service.OnDailyTick();
        var host = _r.SpawnEvil();
        _r.Service.OnPartyDestroyed(host.PartyId);
        _r.Service.LoadFrom("{\"v\":1,\"seen\":44,\"cool\":{\"Free\":300},\"hosts\":[{\"p\":\"p1\",\"h\":\"umbar\",\"s\":\"Neutral\",\"t\":\"empire_w\",\"d\":5,\"r\":2,\"q\":6}]}");
        _r.Service.OnVillageStateChanged(true, "p1");

        _r.Service.ResetForNewSession();

        Assert.AreEqual(_r.Service.CaptureState(), new FireAndTributeRig().Service.CaptureState(), "a new campaign inherits nothing");
        StringAssert.Contains(string.Join("\n", _r.Service.Describe()), "Today: spawned 0, raids 0");
    }
}
