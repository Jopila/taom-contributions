using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>The daily roll and the creation of a host: every gate, the timers, the target, the roster and the failures.</summary>
[TestClass]
public class FireAndTributeSpawnTests
{
    private FireAndTributeRig _r = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99; // no side rolls a host unless a test says so
    }

    private void Tick(int day)
    {
        _r.Snapshots.Day = day;
        _r.Service.OnDailyTick();
    }

    private void OnlyEvilRolls()
    {
        _r.Random.Doubles.Enqueue(0.99); // Free
        _r.Random.Doubles.Enqueue(0.99); // Neutral
        _r.Random.Doubles.Enqueue(0.01); // Evil
    }

    // ---- the window ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Daily_BeforeFullWar_RollsNothingAndSendsNoHost()
    {
        _r.WarOfTheRing.CurrentPhase.Returns(WarPhase.IsengardWar);
        _r.Random.DoubleFallback = 0.0;

        Tick(300);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.Random.DoubleCalls, "a closed window rolls no dice");
    }

    [TestMethod]
    public void Daily_TheFirstHostRidesFortyFiveDaysAfterFullWarBegins()
    {
        Tick(44); // Full War is first seen
        _r.Random.DoubleFallback = 0.0;

        Tick(88);
        Assert.AreEqual(0, _r.Adapter.Parties.Count, "day 88 is one day early");

        Tick(89);
        Assert.AreEqual(3, _r.Adapter.Parties.Count, "day 89: one host per side");
    }

    [TestMethod]
    public void Daily_ASaveFirstSeenInFullWarOnDay300_CountsTheDelayFromThatDay()
    {
        Tick(300);
        _r.Random.DoubleFallback = 0.0;

        Tick(344);
        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Tick(345);
        Assert.AreEqual(3, _r.Adapter.Parties.Count);
    }

    [TestMethod]
    public void Daily_TheFirstSeenDayIsKept_NotMovedByLaterTicks()
    {
        Tick(44);
        Tick(60);

        StringAssert.Contains(_r.Service.CaptureState(), "\"seen\":44");
    }

    [TestMethod]
    public void Daily_BeforeFullWarNothingIsSeen()
    {
        _r.WarOfTheRing.CurrentPhase.Returns(WarPhase.IsengardWar);

        Tick(60);

        StringAssert.Contains(_r.Service.CaptureState(), "\"seen\":-1");
    }

    [TestMethod]
    public void Daily_AfterTheWarEnded_StillSendsHosts()
    {
        _r.WarOfTheRing.CurrentPhase.Returns(WarPhase.WarEnded);
        Tick(44);
        _r.Random.DoubleFallback = 0.0;

        Tick(200);

        Assert.AreEqual(3, _r.Adapter.Parties.Count);
    }

    [TestMethod]
    public void Daily_WithTheWarOfTheRingOff_UsesTheWarlessDay()
    {
        _r.WarOfTheRing.IsEnabled.Returns(false);
        _r.WarOfTheRing.CurrentPhase.Returns(WarPhase.Peace);
        _r.Random.DoubleFallback = 0.0;

        Tick(88);
        Assert.AreEqual(0, _r.Adapter.Parties.Count);

        Tick(89);
        Assert.AreEqual(3, _r.Adapter.Parties.Count);
    }

    // ---- the roll, one per side, the cooldown ----------------------------------------------------------------------

    [TestMethod]
    public void Daily_TheChanceIsFivePercent_ADieAboveItSendsNobody()
    {
        Tick(44);
        _r.Random.DoubleFallback = 0.06;

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(3, _r.Random.DoubleCalls, "one roll per side");
    }

    [TestMethod]
    public void Daily_ADieBelowTheChance_SendsThatSideOnly()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        var host = _r.LiveRecords().Single();
        Assert.AreEqual(FactionSide.Evil, host.Side);
        Assert.AreEqual("gundabad", host.HostId);
    }

    [TestMethod]
    public void Daily_AtMostOneHostPerSide()
    {
        Tick(44);
        _r.Random.DoubleFallback = 0.0;
        Tick(100);
        int first = _r.Adapter.Parties.Count;

        Tick(101);
        Tick(102);

        Assert.AreEqual(3, first);
        Assert.AreEqual(3, _r.Adapter.Parties.Count, "every side already has its host");
        Assert.AreEqual(3, _r.Adapter.Count("spawn:"));
    }

    [TestMethod]
    public void Daily_ASideRestsOneHundredTwentyDaysAfterItsHostIsBroken()
    {
        Tick(44);
        OnlyEvilRolls();
        Tick(100);
        var host = _r.LiveRecords().Single();
        _r.Snapshots.Day = 100;
        _r.Service.OnPartyDestroyed(host.PartyId);
        _r.Random.DoubleFallback = 0.0;
        _r.Adapter.Calls.Clear();

        Tick(219);
        Assert.AreEqual(0, _r.Adapter.Count("spawn:gundabad"), "day 219 is still resting");

        Tick(220);
        Assert.AreEqual(1, _r.Adapter.Count("spawn:gundabad"), "day 220 the side may ride again");
    }

    [TestMethod]
    public void Daily_ASideThatSentAHostHomeAlsoRests()
    {
        Tick(44);
        OnlyEvilRolls();
        Tick(100);
        var host = _r.LiveRecords().Single();
        _r.PartyOf(host).Snapshot.DistanceToHome = 1f;
        // Quota reached, so the host turns home and is dismissed in one step.
        for (int i = 0; i < host.Quota; i++)
            _r.Service.OnVillageStateChanged(true, host.PartyId);
        _r.Service.OnHourlyTick();
        _r.Random.DoubleFallback = 0.0;
        _r.Adapter.Calls.Clear();

        Tick(219);
        Assert.AreEqual(0, _r.Adapter.Count("spawn:gundabad"));
        Tick(220);
        Assert.AreEqual(1, _r.Adapter.Count("spawn:gundabad"));
    }

    // ---- switches and the single-player gate -----------------------------------------------------------------------

    [TestMethod]
    public void Daily_TheMasterSwitchOff_SendsNobodyAndRollsNothing()
    {
        Tick(44);
        _r.Settings.Enabled = false;
        _r.Random.DoubleFallback = 0.0;

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.Random.DoubleCalls);
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free)]
    [DataRow(FactionSide.Neutral)]
    [DataRow(FactionSide.Evil)]
    public void Daily_ASideSwitchOff_SendsThatSideNoHost_AndTheOthersStillRide(FactionSide off)
    {
        Tick(44);
        _r.Settings.FreeHostsEnabled = off != FactionSide.Free;
        _r.Settings.NeutralHostsEnabled = off != FactionSide.Neutral;
        _r.Settings.EvilHostsEnabled = off != FactionSide.Evil;
        _r.Random.DoubleFallback = 0.0;

        Tick(100);

        var sides = _r.LiveRecords().Select(r => r.Side).ToList();
        Assert.AreEqual(2, sides.Count);
        CollectionAssert.DoesNotContain(sides, off);
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void Daily_InCoopOrOnADedicatedServer_SendsNobodyAndRollsNothing(bool session, bool defer, bool dedicated)
    {
        Tick(44);
        _r.Coop.IsSessionActive.Returns(session);
        _r.Coop.ShouldDeferToHost.Returns(defer);
        _r.Dedicated.IsDedicatedServer.Returns(dedicated);
        _r.Random.DoubleFallback = 0.0;

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.Random.DoubleCalls);
    }

    [TestMethod]
    public void Daily_AnUnreadableCampaignDay_DoesNothing()
    {
        _r.Random.DoubleFallback = 0.0;
        _r.Snapshots.DayReadable = false;

        _r.Service.OnDailyTick();

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.Random.DoubleCalls);
        _r.Logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void Daily_NoConfiguredHosts_SendsNobody()
    {
        _r.ConfigProvider.Config = new FireAndTributeConfig();
        Tick(44);
        _r.Random.DoubleFallback = 0.0;

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
    }

    // ---- target ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Spawn_EachSideTargetsTheStrongestKingdomOfItsTargetSides()
    {
        Tick(44);
        _r.Random.DoubleFallback = 0.0;

        Tick(100);

        var byHost = _r.LiveRecords().ToDictionary(r => r.HostId, r => r.TargetKingdomId);
        Assert.AreEqual("empire_w", byHost["gundabad"], "an Evil host raids the strongest Free realm");
        Assert.AreEqual("empire_s", byHost["erebor"], "a Free host raids the strongest Evil realm");
        Assert.AreEqual("empire_s", byHost["umbar"], "a Neutral host raids the strongest Free or Evil realm, never the Neutral one");
    }

    [TestMethod]
    public void Spawn_AKingdomWithNoRaidableVillage_IsNotATarget()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual("vlandia", _r.LiveRecords().Single().TargetKingdomId);
    }

    [TestMethod]
    public void Spawn_AKingdomWhoseOnlyVillagesAreLootedOrPoor_IsNotATarget()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        _r.Adapter.Put("w_looted", "empire_w", normal: false);
        _r.Adapter.Put("w_poor", "empire_w", hearth: 149f);
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual("vlandia", _r.LiveRecords().Single().TargetKingdomId);
    }

    [TestMethod]
    public void Spawn_NoRaidableKingdomAtAll_SendsNobody()
    {
        _r.Adapter.Villages.Clear();
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.Adapter.Count("spawn:"));
    }

    [TestMethod]
    public void Spawn_WithNoPreference_TheStrongestRaidableKingdomIsRaided()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual("empire_w", _r.LiveRecords().Single().TargetKingdomId, "12000 against vlandia's 8000");
    }

    [TestMethod]
    public void Spawn_APreferredTargetBeatsTheStrongest()
    {
        var config = FireAndTributeRig.DefaultConfig();
        ((RaidHostDef)config.Hosts.Single(h => h.Id == "gundabad")).PreferredTargets = new[] { "vlandia" };
        _r.ConfigProvider.Config = config;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual("vlandia", _r.LiveRecords().Single().TargetKingdomId);
    }

    // ---- size, quota, and what the engine is asked to do -----------------------------------------------------------

    [TestMethod]
    public void Spawn_FillsTheRosterThenFinishesThePartyAndNamesIt()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        var host = _r.LiveRecords().Single();
        var party = _r.PartyOf(host);
        Assert.AreEqual(102, party.Roster.Values.Sum(), "12000 strength * 0.012 split by share");
        Assert.AreEqual(72, party.Roster["snaga"]);
        Assert.IsTrue(party.Completed);
        Assert.AreEqual("{=taom_fat_host_gundabad}The Host of Gundabad", party.Name);
        Assert.AreEqual("gundabad_raiders", party.ClanId);
        int lastAdd = _r.Adapter.Calls.FindLastIndex(c => c.StartsWith("add:", StringComparison.Ordinal));
        Assert.IsTrue(_r.Adapter.Calls.IndexOf("complete:" + host.PartyId) > lastAdd, "finished after the last troop");
        Assert.IsTrue(_r.Adapter.Calls.Any(c => c == "spawn:gundabad_raiders:town_G1:0.25"), "the first anchor, the data's spawn radius");
    }

    [TestMethod]
    public void Spawn_ASmallKingdomStillGetsTheMinimumHost()
    {
        _r.Snapshots.Kingdoms.Single(k => k.Id == "empire_w").Strength = 100f;
        _r.Snapshots.Kingdoms.Single(k => k.Id == "vlandia").Strength = 50f;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(80, _r.PartyOf(_r.LiveRecords().Single()).Roster.Values.Sum());
    }

    [TestMethod]
    public void Spawn_AHugeKingdomIsCappedAtTheMaximumHost()
    {
        _r.Snapshots.Kingdoms.Single(k => k.Id == "empire_w").Strength = 5_000_000f;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(200, _r.PartyOf(_r.LiveRecords().Single()).Roster.Values.Sum());
    }

    [TestMethod]
    public void Spawn_TheQuotaIsTheTargetStrengthOverTwoThousandPlusTheRolledExtra()
    {
        Tick(44);
        OnlyEvilRolls();
        _r.Random.Ints.Enqueue(0); // the anchor
        _r.Random.Ints.Enqueue(3); // the extra, 0 to 3

        Tick(100);

        Assert.AreEqual(9, _r.LiveRecords().Single().Quota, "round(12000 / 2000) + 3");
    }

    [TestMethod]
    public void Spawn_TheQuotaIsClampedToTheCap()
    {
        _r.Snapshots.Kingdoms.Single(k => k.Id == "empire_w").Strength = 500_000f;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(15, _r.LiveRecords().Single().Quota);
    }

    [TestMethod]
    public void Spawn_TellsThePlayerWithTheHostsNameAndTheNearestTown()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        CollectionAssert.Contains(_r.Notices.Lines, "spawned:{=taom_fat_host_gundabad}The Host of Gundabad:town_X");
    }

    [TestMethod]
    public void Spawn_TheNewHostStartsRaidingAtOnce()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.IsTrue(_r.Adapter.Calls.Any(c => c.StartsWith("raid:host_1:empire_w_v", StringComparison.Ordinal)), "an order in the spawn hour");
    }

    [TestMethod]
    public void Spawn_RecordsTheStartDayTargetAndNoRaidsYet()
    {
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        var host = _r.LiveRecords().Single();
        Assert.AreEqual(100, host.StartDay);
        Assert.AreEqual(0, host.Raids);
        Assert.IsFalse(host.Returning);
    }

    // ---- failures --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Spawn_AMissingClan_SkipsTheHostWithOneWarning()
    {
        _r.Adapter.ProbeByClan["gundabad_raiders"] = RaidSpawnFailure.ClanMissing;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(1, _r.Warnings("gundabad skipped"));
        Assert.AreEqual(1, _r.Adapter.Count("probe:"), "another anchor would fail the same way");
    }

    [TestMethod]
    public void Spawn_NoHideoutOfTheClansCulture_SkipsTheHostWithOneWarning()
    {
        _r.Adapter.ProbeByClan["gundabad_raiders"] = RaidSpawnFailure.NoHideout;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(1, _r.Warnings("NoHideout"));
    }

    [TestMethod]
    public void Spawn_AMissingAnchor_TriesTheNextOne()
    {
        _r.Adapter.ProbeByAnchor["town_G1"] = RaidSpawnFailure.AnchorMissing;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.IsTrue(_r.Adapter.Called("spawn:gundabad_raiders:castle_G4:0.25"));
    }

    [TestMethod]
    public void Spawn_EveryAnchorMissing_SkipsTheHost()
    {
        _r.Adapter.ProbeByAnchor["town_G1"] = RaidSpawnFailure.AnchorMissing;
        _r.Adapter.ProbeByAnchor["castle_G4"] = RaidSpawnFailure.AnchorMissing;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(1, _r.Warnings("AnchorMissing"));
    }

    // #1032: an anchor the other side has captured is skipped, as a missing one is.
    [TestMethod]
    public void Spawn_AnAnchorHeldByTheOtherSide_TriesTheNextOne()
    {
        _r.Adapter.AnchorOwners["town_G1"] = ("empire_w", "empire_w", true);
        _r.Adapter.AnchorOwners["castle_G4"] = ("empire_s", "empire_s", true);
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.IsTrue(_r.Adapter.Called("spawn:gundabad_raiders:castle_G4:0.25"));
        Assert.IsFalse(_r.Adapter.Called("spawn:gundabad_raiders:town_G1:0.25"));
    }

    [TestMethod]
    public void Spawn_EveryAnchorHeldByTheOtherSide_SkipsTheHost()
    {
        _r.Adapter.AnchorOwners["town_G1"] = ("empire_w", "empire_w", true);
        _r.Adapter.AnchorOwners["castle_G4"] = ("vlandia", "vlandia", true);
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(1, _r.Warnings("AnchorHeldByOtherSide"));
    }

    [TestMethod]
    public void Spawn_AnchorsHeldByTheOwnSide_SpawnAsBefore()
    {
        _r.Adapter.AnchorOwners["town_G1"] = ("empire_s", "empire_s", true);
        _r.Adapter.AnchorOwners["castle_G4"] = ("isengard", "isengard", true);
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(1, _r.LiveRecords().Count);
    }

    // A rebel clan holds the town with the town's own culture and no kingdom: its parties are not held back by the
    // own-side rule, so the anchor counts as lost even though the culture resolves to the host's side.
    [TestMethod]
    public void Spawn_AnAnchorHeldByARebelClanOfTheHostsCulture_TriesTheNextOne()
    {
        _r.Adapter.AnchorOwners["town_G1"] = ("town_G1_rebel_clan", "empire_s", false);
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.IsTrue(_r.Adapter.Called("spawn:gundabad_raiders:castle_G4:0.25"));
        Assert.IsFalse(_r.Adapter.Called("spawn:gundabad_raiders:town_G1:0.25"));
    }

    [TestMethod]
    public void Spawn_ANeutralHostAtANeutralAnchor_Spawns()
    {
        _r.Adapter.AnchorOwners["town_U1"] = ("umbar", "umbar", true);

        var result = _r.Service.TrySpawn(FactionSide.Neutral, dryRun: false);

        Assert.AreEqual(1, _r.LiveRecords().Count(h => h.Side == FactionSide.Neutral), result);
    }

    [TestMethod]
    public void Spawn_NoPointOutOfSight_SkipsTheHostAndWarns()
    {
        _r.Adapter.SpawnFailure = RaidSpawnFailure.NoPoint;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(1, _r.Warnings("NoPoint"));
        Assert.AreEqual(0, _r.Notices.Lines.Count);
    }

    [TestMethod]
    public void Spawn_AHostThatCannotGo_LetsTheNextHostOfTheSideRide()
    {
        var config = FireAndTributeRig.DefaultConfig();
        var hosts = config.Hosts.ToList();
        hosts.Insert(0, new RaidHostDef
        {
            Id = "dunland", Side = FactionSide.Evil, ClanId = "dunland_raiders", Name = "{=taom_fat_host_dunland}The Dunlending Host",
            Anchors = new[] { "town_EN3" }, TargetSides = new[] { FactionSide.Free }, PreferredTargets = new string[0],
            Troops = new[] { new HostTroop("snaga", 1f) },
        });
        config.Hosts = hosts;
        _r.ConfigProvider.Config = config;
        _r.Adapter.ProbeByClan["dunland_raiders"] = RaidSpawnFailure.ClanMissing;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual("gundabad", _r.LiveRecords().Single().HostId);
        Assert.AreEqual(1, _r.Warnings("dunland skipped"));
    }

    [TestMethod]
    public void Spawn_AHostWithNoKnownTroop_IsSkippedWithAWarning()
    {
        _r.Adapter.Power.Clear();
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(1, _r.Warnings("usable power"));
    }

    [TestMethod]
    public void Spawn_WhenNoTroopCanBeAdded_TheEmptyPartyIsRemovedQuietly()
    {
        _r.Adapter.AddTroopsAddsNothing = true;
        Tick(44);
        OnlyEvilRolls();

        Tick(100);

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count, "no rumour, and no 'broken' line for a party that never rode");
        Assert.IsTrue(_r.Adapter.Calls.Contains("despawn:host_1"));
    }

    [TestMethod]
    public void Spawn_WhenFillingThrows_TheHalfBuiltPartyIsRemovedAndTheErrorSurfaces()
    {
        _r.Adapter.AddTroopsThrows = true;
        Tick(44);
        OnlyEvilRolls();

        Assert.ThrowsException<InvalidOperationException>(() => Tick(100));

        Assert.AreEqual(0, _r.Adapter.Parties.Count);
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    // ---- diagnostics -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Daily_WritesTheDebugLine_ThenResetsTheDayCounters()
    {
        Tick(44);
        OnlyEvilRolls();
        Tick(100);
        var host = _r.LiveRecords().Single();
        _r.Service.OnVillageStateChanged(true, host.PartyId);
        _r.Service.OnVillageStateChanged(true, host.PartyId);

        Tick(101);
        Tick(102);

        _r.Logger.Received().LogDebug(Arg.Is<string>(s => s.StartsWith("[FireAndTribute] day 100: hosts=1, spawned=1, raids=0, maxHourMs=")));
        _r.Logger.Received().LogDebug(Arg.Is<string>(s => s.StartsWith("[FireAndTribute] day 101: hosts=1, spawned=0, raids=2, maxHourMs=")));
        _r.Logger.Received().LogDebug(Arg.Is<string>(s => s.StartsWith("[FireAndTribute] day 102: hosts=1, spawned=0, raids=0, maxHourMs=")));
    }

    [TestMethod]
    public void Daily_TheDebugLineUsesTheInvariantCulture()
    {
        var old = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Tick(50);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = old;
        }

        _r.Logger.Received().LogDebug(Arg.Is<string>(s => s.Contains("maxHourMs=0.000")));
    }
}
