using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>The hourly step of a host, branch by branch, and the events that end one.</summary>
[TestClass]
public class FireAndTributeSteeringTests
{
    private FireAndTributeRig _r = null!;
    private RaidHostRecord _host = null!;
    private FatParty _party = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
        _host = _r.SpawnEvil(); // target empire_w, quota 6, spawned on day 100
        _party = _r.PartyOf(_host);
        _party.Snapshot.RaidTargetId = null;
        _r.Adapter.Calls.Clear();
        _r.Notices.Lines.Clear();
    }

    private void Hour(int day = 100)
    {
        _r.Snapshots.Day = day;
        _r.Service.OnHourlyTick();
    }

    private RaidHostRecord Record() => _r.LiveRecords().Single();

    private static NearbyParty Prey(string id = "prey", string kingdom = "empire_w", float strength = 100f, float speed = 4f, float distance = 3f) =>
        new NearbyParty { Id = id, KingdomId = kingdom, Strength = strength, Speed = speed, Distance = distance };

    // ---- skipped hours ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void Hour_NoHost_TouchesNothing()
    {
        var rig = new FireAndTributeRig();

        rig.Service.OnHourlyTick();

        Assert.AreEqual(0, rig.Adapter.Calls.Count);
    }

    [TestMethod]
    public void Hour_AHostInAMapEvent_IsLeftAlone()
    {
        _party.Snapshot.InMapEvent = true;

        Hour();

        Assert.IsFalse(_r.Adapter.Calls.Any(c => c.StartsWith("raid:") || c.StartsWith("engage:") || c.StartsWith("home:") || c.StartsWith("despawn:")));
    }

    [TestMethod]
    public void Hour_AnInactiveHost_IsLeftAlone()
    {
        _party.Snapshot.IsActive = false;

        Hour();

        Assert.IsFalse(_r.Adapter.Calls.Any(c => c.StartsWith("raid:") || c.StartsWith("engage:") || c.StartsWith("home:") || c.StartsWith("despawn:")));
        Assert.AreEqual(1, _r.LiveRecords().Count, "still tracked");
    }

    [TestMethod]
    public void Hour_APartyTheEngineLost_DropsTheRecordAndRestsTheSide_WithoutANotice()
    {
        _r.Adapter.Parties.Remove(_host.PartyId);

        Hour();

        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
        Assert.AreEqual(1, _r.Warnings("no longer on the map"));
    }

    [TestMethod]
    public void Hour_AnUnreadableDay_DoesNothing()
    {
        _r.Snapshots.DayReadable = false;

        _r.Service.OnHourlyTick();

        Assert.AreEqual(0, _r.Adapter.Calls.Count);
    }

    [TestMethod]
    public void Hour_ReadsEachHostOnce()
    {
        var rig = new FireAndTributeRig();
        rig.Random.DoubleFallback = 0.0;
        rig.Snapshots.Day = 44;
        rig.Service.OnDailyTick();
        rig.Snapshots.Day = 100;
        rig.Service.OnDailyTick();
        Assert.AreEqual(3, rig.LiveRecords().Count);
        rig.Adapter.Calls.Clear();

        rig.Service.OnHourlyTick();

        Assert.AreEqual(3, rig.Adapter.Count("read:"));
    }

    // ---- raiding ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Hour_PicksOneOfTheNearestVillages_AtRandom()
    {
        _r.Random.Ints.Enqueue(2);

        Hour();

        Assert.IsTrue(_r.Adapter.Called("villages"), "reads the village list live");
        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:empire_w_v3"));
    }

    [TestMethod]
    public void Hour_NeverPicksBeyondTheNearestThree_HoweverTheDieFalls()
    {
        _r.Adapter.Put("empire_w_v4", "empire_w", x: 104f);
        _r.Adapter.Put("empire_w_v5", "empire_w", x: 105f);
        _r.Random.Ints.Enqueue(4);

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:empire_w_v3"), "the last of the three nearest");
        Assert.AreEqual(0, _r.Adapter.Count($"raid:{_host.PartyId}:empire_w_v4") + _r.Adapter.Count($"raid:{_host.PartyId}:empire_w_v5"));
    }

    [TestMethod]
    public void Hour_PicksTheNearestByDistance_NotByTheOrderTheEngineListsThem()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        _r.Adapter.Put("far", "empire_w", x: 160f);
        _r.Adapter.Put("near", "empire_w", x: 101f);
        _r.Adapter.Put("mid", "empire_w", x: 120f);
        _r.Random.Ints.Enqueue(0);

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:near"));
    }

    [TestMethod]
    public void Hour_APickIsNeverOutsideTheNearestList()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        _r.Adapter.Put("only_one", "empire_w");
        _r.Random.Ints.Enqueue(2);

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:only_one"));
    }

    [TestMethod]
    public void Hour_KeepsAValidRaidOrder_AndAsksForNoNewVillage()
    {
        _party.Snapshot.RaidTargetId = "empire_w_v2";

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("raid:"));
        Assert.AreEqual(0, _r.Adapter.Count("villages"));
        Assert.IsTrue(_r.Adapter.Called("village:empire_w_v2"));
    }

    [DataTestMethod]
    [DataRow("looted")]
    [DataRow("other kingdom")]
    [DataRow("low hearth")]
    [DataRow("nan hearth")]
    [DataRow("gone")]
    public void Hour_AnInvalidRaidOrder_IsReplacedByANewVillage(string why)
    {
        _party.Snapshot.RaidTargetId = "stale";
        switch (why)
        {
            case "looted": _r.Adapter.Put("stale", "empire_w", normal: false); break;
            case "other kingdom": _r.Adapter.Put("stale", "vlandia"); break;
            case "low hearth": _r.Adapter.Put("stale", "empire_w", hearth: 149f); break;
            case "nan hearth": _r.Adapter.Put("stale", "empire_w", hearth: float.NaN); break;
        }

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:empire_w_v1"), why);
    }

    [TestMethod]
    public void Hour_AVillageAtExactlyTheMinimumHearth_StillCounts()
    {
        _party.Snapshot.RaidTargetId = "edge";
        _r.Adapter.Put("edge", "empire_w", hearth: 150f);

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("raid:"));
    }

    [TestMethod]
    public void Hour_NoRaidableVillageLeft_SendsTheHostHome()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");

        Hour();

        Assert.IsTrue(Record().Returning);
        Assert.IsTrue(_r.Adapter.Called($"home:{_host.PartyId}"));
        CollectionAssert.Contains(_r.Notices.Lines, "going:{=taom_fat_host_gundabad}The Host of Gundabad");
    }

    [TestMethod]
    public void Hour_TheQuotaReached_SendsTheHostHome()
    {
        for (int i = 0; i < _host.Quota; i++)
            _r.Service.OnVillageStateChanged(true, _host.PartyId);

        Hour();

        Assert.IsTrue(Record().Returning);
        Assert.IsTrue(_r.Adapter.Called($"home:{_host.PartyId}"));
        Assert.AreEqual(0, _r.Adapter.Count("raid:"));
    }

    [TestMethod]
    public void Hour_OneRaidShortOfTheQuota_KeepsRaiding()
    {
        for (int i = 0; i < _host.Quota - 1; i++)
            _r.Service.OnVillageStateChanged(true, _host.PartyId);

        Hour();

        Assert.IsFalse(Record().Returning);
        Assert.AreEqual(1, _r.Adapter.Count("raid:"));
    }

    [TestMethod]
    public void Hour_ThirtyDaysOut_SendsTheHostHomeWhateverItsQuota()
    {
        Hour(129);
        Assert.IsFalse(Record().Returning, "day 129 is 29 days in");

        Hour(130);

        Assert.IsTrue(Record().Returning);
        Assert.IsTrue(_r.Adapter.Called($"home:{_host.PartyId}"));
    }

    [TestMethod]
    public void Hour_TurningHome_TellsThePlayerOnce()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");

        Hour();
        Hour();
        Hour();

        Assert.AreEqual(1, _r.Notices.Lines.Count(l => l.StartsWith("going:")));
    }

    // ---- engaging --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Hour_EngagesAWeakerSlowerPartyOfTheTargetKingdom_BeforeAnyVillage()
    {
        _r.Adapter.Nearby.Add(Prey());

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:prey"));
        Assert.AreEqual(0, _r.Adapter.Count("raid:"));
        Assert.IsTrue(_r.Adapter.Called($"near:{_host.PartyId}:8"), "looks within the engage radius");
    }

    [TestMethod]
    public void Hour_EngagesTheNearestOfSeveral_TiesByOrdinalId()
    {
        _r.Adapter.Nearby.Add(Prey("far", distance: 6f));
        _r.Adapter.Nearby.Add(Prey("b_near", distance: 2f));
        _r.Adapter.Nearby.Add(Prey("a_near", distance: 2f));

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:a_near"));
    }

    [DataTestMethod]
    [DataRow("other kingdom")]
    [DataRow("in battle")]
    [DataRow("in settlement")]
    [DataRow("attached")]
    [DataRow("too strong")]
    [DataRow("nan strength")]
    [DataRow("faster")]
    [DataRow("nan speed")]
    [DataRow("beyond radius")]
    [DataRow("nan distance")]
    public void Hour_NeverEngagesAnUnfitParty(string why)
    {
        var prey = Prey();
        switch (why)
        {
            case "other kingdom": prey.KingdomId = "vlandia"; break;
            case "in battle": prey.InMapEvent = true; break;
            case "in settlement": prey.InSettlement = true; break;
            case "attached": prey.IsAttached = true; break;
            case "too strong": prey.Strength = 161f; break; // 0.8 x 200 = 160
            case "nan strength": prey.Strength = float.NaN; break;
            case "faster": prey.Speed = 5.01f; break;
            case "nan speed": prey.Speed = float.NaN; break;
            case "beyond radius": prey.Distance = 8.1f; break;
            default: prey.Distance = float.NaN; break;
        }
        _r.Adapter.Nearby.Add(prey);

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("engage:"), why);
        Assert.AreEqual(1, _r.Adapter.Count("raid:"), "it raids instead: " + why);
    }

    [TestMethod]
    public void Hour_ThePlayersPartyIsEngagedOnlyWhenItBelongsToTheTargetKingdom()
    {
        _r.Adapter.Nearby.Add(Prey("main", kingdom: "empire_s")); // the player serves Mordor, the host raids Gondor
        Hour();
        Assert.AreEqual(0, _r.Adapter.Count("engage:"));

        _r.Adapter.Nearby.Clear();
        _r.Adapter.Nearby.Add(Prey("main", kingdom: "empire_w")); // the player serves the target
        Hour();
        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:main"));
    }

    [TestMethod]
    public void Hour_StrengthRatioEdgeIsInclusive()
    {
        _r.Adapter.Nearby.Add(Prey(strength: 160f));

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:prey"));
    }

    [TestMethod]
    public void Hour_TheServiceComparesTheStrengthTheAdapterReports_SoAHeavyReadingIsNotEngagedAndALightOneIs()
    {
        // The service compares the strength the adapter reports (an army leader's whole army, pinned in
        // RaidHostAdapterIlTests) and knows nothing of armies. A reading of 300 is over the host's 160 limit.
        var leader = Prey("leader", strength: 300f);
        _r.Adapter.Nearby.Add(leader);

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("engage:"));
        Assert.AreEqual(1, _r.Adapter.Count("raid:"), "it raids instead");

        leader.Strength = 150f; // an army the host can take
        Hour();
        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:leader"));
    }

    [TestMethod]
    public void Hour_ReturningHostsEngageNobody()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        Hour(); // turns home
        _r.Adapter.Calls.Clear();
        _r.Adapter.Nearby.Add(Prey());

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("engage:"));
        Assert.AreEqual(0, _r.Adapter.Count("near:"));
    }

    [TestMethod]
    public void Hour_AHostWithNoReadableStrengthOrSpeed_EngagesNobody()
    {
        _party.Snapshot.Strength = float.NaN;
        _r.Adapter.Nearby.Add(Prey());
        Hour();
        Assert.AreEqual(0, _r.Adapter.Count("engage:"));

        _party.Snapshot.Strength = 200f;
        _party.Snapshot.Speed = float.NaN;
        Hour();
        Assert.AreEqual(0, _r.Adapter.Count("engage:"));
    }

    // ---- going home ------------------------------------------------------------------------------------------------

    private void TurnHome()
    {
        _r.Adapter.RemoveVillagesOf("empire_w");
        Hour();
        _r.Adapter.Calls.Clear();
        _r.Notices.Lines.Clear();
    }

    [TestMethod]
    public void Hour_AReturningHostFarFromHome_IsOrderedHomeEveryHour()
    {
        TurnHome();

        Hour();
        Hour();

        Assert.AreEqual(2, _r.Adapter.Count("home:"));
        Assert.AreEqual(0, _r.Adapter.Count("despawn:"));
    }

    [TestMethod]
    public void Hour_AReturningHostWithinTheHomeRadius_IsDismissed_AndThePlayerToldItWentHome()
    {
        TurnHome();
        _party.Snapshot.DistanceToHome = 4f;

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
        CollectionAssert.AreEqual(new[] { "home:{=taom_fat_host_gundabad}The Host of Gundabad" }, _r.Notices.Lines);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
    }

    [TestMethod]
    public void Hour_AReturningHostJustOutsideTheHomeRadius_KeepsWalking()
    {
        TurnHome();
        _party.Snapshot.DistanceToHome = 4.01f;

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("despawn:"));
        Assert.AreEqual(1, _r.Adapter.Count("home:"));
    }

    [TestMethod]
    public void Hour_AReturningHostWithAnUnreadableDistance_KeepsWalking()
    {
        TurnHome();
        _party.Snapshot.DistanceToHome = float.NaN;

        Hour();

        Assert.AreEqual(0, _r.Adapter.Count("despawn:"));
        Assert.AreEqual(1, _r.Adapter.Count("home:"));
    }

    [TestMethod]
    public void Hour_AReturningHostOutTwiceTheTimeout_IsDismissedWhereItStands()
    {
        TurnHome(); // turned for home on day 100, 60 days is twice the 30-day timeout

        Hour(159);
        Assert.AreEqual(0, _r.Adapter.Count("despawn:"), "59 days out: still walking");

        Hour(160);
        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void Hour_AReturningHostWithNoHomeLeft_IsDismissedAtOnce()
    {
        TurnHome();
        _party.Snapshot.HasHome = false;

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
    }

    [TestMethod]
    public void Hour_ADismissalTheEngineRefuses_IsRetriedNextHour_WithNoNotice()
    {
        TurnHome();
        _party.Snapshot.DistanceToHome = 1f;
        _r.Adapter.DespawnRefuses = true;

        Hour();

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);

        _r.Adapter.DespawnRefuses = false;
        Hour();
        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(1, _r.Notices.Lines.Count);
    }

    [TestMethod]
    public void Hour_ADismissalIsPlanned_SoItsDestroyEventIsNotAReportedBreak()
    {
        TurnHome();
        _party.Snapshot.DistanceToHome = 1f;

        Hour();

        Assert.AreEqual(0, _r.Notices.Lines.Count(l => l.StartsWith("broken:")));
        Assert.AreEqual(1, _r.Notices.Lines.Count(l => l.StartsWith("home:")));
    }

    // ---- events ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void VillageStateChanged_CountsALootByTheHost()
    {
        _r.Service.OnVillageStateChanged(true, _host.PartyId);
        _r.Service.OnVillageStateChanged(true, _host.PartyId);

        Assert.AreEqual(2, Record().Raids);
    }

    [TestMethod]
    public void VillageStateChanged_IgnoresAnyStateButLooted()
    {
        _r.Service.OnVillageStateChanged(false, _host.PartyId);

        Assert.AreEqual(0, Record().Raids);
    }

    [TestMethod]
    public void VillageStateChanged_IgnoresALootByAnyoneElse_AndANullRaider()
    {
        _r.Service.OnVillageStateChanged(true, "lord_party_9");
        _r.Service.OnVillageStateChanged(true, null);
        _r.Service.OnVillageStateChanged(true, string.Empty);

        Assert.AreEqual(0, Record().Raids);
    }

    [TestMethod]
    public void VillageStateChanged_OnlyRecords_ItGivesNoOrdersAndNoNotice()
    {
        _r.Service.OnVillageStateChanged(true, _host.PartyId);

        Assert.AreEqual(0, _r.Adapter.Calls.Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);
    }

    [TestMethod]
    public void PartyDestroyed_ByTheEngine_IsABrokenHost_TheSideRests()
    {
        _r.Service.OnPartyDestroyed(_host.PartyId);

        Assert.AreEqual(0, _r.LiveRecords().Count);
        CollectionAssert.AreEqual(new[] { "broken:{=taom_fat_host_gundabad}The Host of Gundabad" }, _r.Notices.Lines);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
    }

    [TestMethod]
    public void PartyDestroyed_ForAnotherParty_OrNull_ChangesNothing()
    {
        _r.Service.OnPartyDestroyed("somebody_else");
        _r.Service.OnPartyDestroyed(null);

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);
    }

    [TestMethod]
    public void PartyDestroyed_WithAnUnreadableDay_StillDropsTheRecord()
    {
        _r.Snapshots.DayReadable = false;

        _r.Service.OnPartyDestroyed(_host.PartyId);

        Assert.AreEqual(0, _r.LiveRecords().Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{}", "no cooldown from a clock that cannot be read");
    }

    // ---- ending a host: the pin is released or the party removed, on every path -------------------------------------

    [TestMethod]
    public void SwitchOff_RetiresALiveHostAtItsNextHour_Quietly()
    {
        _r.Settings.EvilHostsEnabled = false;

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Notices.Lines.Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{}", "a switch is not a defeat");
    }

    [TestMethod]
    public void MasterSwitchOff_RetiresEveryHost()
    {
        _r.Settings.Enabled = false;

        Hour();

        Assert.AreEqual(0, _r.LiveRecords().Count);
        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
    }

    [TestMethod]
    public void SwitchOff_WhileTheHostIsFighting_KeepsItsRecordAndItsPin_AndRetriesNextHour()
    {
        _r.Settings.Enabled = false;
        _party.Snapshot.InMapEvent = true;

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"), "it tries to remove the host");
        Assert.AreEqual(0, _r.Adapter.Count("release:"), "a pinned host that roams on is worse than one still tracked");
        Assert.AreEqual(1, _r.LiveRecords().Count, "the record stays while the party is in the battle");
        Assert.IsTrue(_r.Adapter.Parties.ContainsKey(_host.PartyId));

        _party.Snapshot.InMapEvent = false;
        _r.Adapter.Calls.Clear();
        Hour();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count, "removed once the battle is over");
        Assert.IsFalse(_r.Adapter.Parties.ContainsKey(_host.PartyId));
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{}", "a switch is not a defeat");
    }

    [TestMethod]
    public void SwitchOff_ThenOn_WhileTheHostFights_CannotProduceASecondHost()
    {
        _r.Snapshots.Day = 44;
        _r.Service.OnDailyTick(); // Full War is first seen on day 44, so the window opens on day 89
        _r.Random.DoubleFallback = 0.0; // every side that may roll, rolls a host
        _party.Snapshot.InMapEvent = true;
        _r.Settings.EvilHostsEnabled = false;
        Hour(200);
        _r.Settings.EvilHostsEnabled = true;

        _r.Snapshots.Day = 200;
        _r.Service.OnDailyTick();
        _r.Snapshots.Day = 201;
        _r.Service.OnDailyTick();

        var evil = _r.LiveRecords().Where(r => r.Side == FactionSide.Evil).ToList();
        Assert.AreEqual(1, evil.Count);
        Assert.AreEqual(_host.PartyId, evil[0].PartyId, "still the first host");
        Assert.AreEqual(0, _r.Adapter.Count("spawn:gundabad"), "no second Gundabad party was created");
    }

    [TestMethod]
    public void SwitchOff_ForAPartyTheEngineLost_DropsTheRecordAndRestsTheSide()
    {
        _r.Settings.Enabled = false;
        _r.Adapter.Parties.Remove(_host.PartyId);

        Hour();

        Assert.AreEqual(0, _r.LiveRecords().Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
        Assert.AreEqual(1, _r.Warnings("no longer on the map"));
    }

    [TestMethod]
    public void SwitchOff_AnInactiveHost_IsKeptAndRetried()
    {
        _r.Settings.Enabled = false;
        _party.Snapshot.IsActive = false;

        Hour();
        Hour();

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(2, _r.Adapter.Count("despawn:"));
        Assert.AreEqual(0, _r.Adapter.Count("release:"));
    }

    [TestMethod]
    public void Hour_OneHostsFailingStep_IsLogged_AndTheOtherHostsStillSteer()
    {
        var rig = new FireAndTributeRig();
        rig.Random.DoubleFallback = 0.0;
        rig.Snapshots.Day = 44;
        rig.Service.OnDailyTick();
        rig.Snapshots.Day = 100;
        rig.Service.OnDailyTick();
        var records = rig.LiveRecords();
        Assert.AreEqual(3, records.Count);
        foreach (var record in records)
            rig.PartyOf(record).Snapshot.RaidTargetId = null;
        rig.Adapter.ReadThrows.Add(records[0].PartyId);
        rig.Adapter.Calls.Clear();

        rig.Service.OnHourlyTick();

        Assert.AreEqual(2, rig.Adapter.Count("raid:"), "the two healthy hosts were ordered to raid");
        Assert.IsFalse(rig.Adapter.Calls.Any(c => c.StartsWith($"raid:{records[0].PartyId}:", StringComparison.Ordinal)), "the failing host got no order");
        rig.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[FireAndTribute]") && s.Contains(records[0].HostId) && s.Contains("read failed")));
        Assert.AreEqual(3, rig.LiveRecords().Count, "the failing host stays tracked");
    }

    [TestMethod]
    public void Hour_TheFailingHostIsLastOrFirst_NeverStopsTheLoop()
    {
        var rig = new FireAndTributeRig();
        rig.Random.DoubleFallback = 0.0;
        rig.Snapshots.Day = 44;
        rig.Service.OnDailyTick();
        rig.Snapshots.Day = 100;
        rig.Service.OnDailyTick();
        var records = rig.LiveRecords();
        rig.Adapter.ReadThrows.Add(records[2].PartyId);
        rig.Adapter.Calls.Clear();

        rig.Service.OnHourlyTick();

        Assert.AreEqual(3, rig.Adapter.Count("read:"), "every host was read");
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void Hour_InCoopOrOnADedicatedServer_ReleasesThePinAndSteersNothing(bool session, bool defer, bool dedicated)
    {
        _r.Coop.IsSessionActive.Returns(session);
        _r.Coop.ShouldDeferToHost.Returns(defer);
        _r.Dedicated.IsDedicatedServer.Returns(dedicated);

        Hour();

        Assert.IsTrue(_r.Adapter.Called($"release:{_host.PartyId}"));
        Assert.AreEqual(0, _r.Adapter.Count("despawn:"), "never removes a party in a shared session");
        Assert.AreEqual(0, _r.Adapter.Count("raid:") + _r.Adapter.Count("engage:") + _r.Adapter.Count("home:"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void Hour_InCoop_RestsTheReleasedHostsSide_SoTheNextSessionDoesNotStackAHost()
    {
        _r.Coop.IsSessionActive.Returns(true);

        Hour();

        Assert.AreEqual(0, _r.LiveRecords().Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
    }

    [TestMethod]
    public void Daily_InCoop_ReleasesThePinToo()
    {
        _r.Coop.IsSessionActive.Returns(true);

        _r.Service.OnDailyTick();

        Assert.IsTrue(_r.Adapter.Called($"release:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void SessionLaunched_InCoop_ReleasesARestoredHost()
    {
        _r.Coop.ShouldDeferToHost.Returns(true);

        _r.Service.OnSessionLaunched();

        Assert.IsTrue(_r.Adapter.Called($"release:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void SessionLaunched_SinglePlayer_KeepsALiveHost_AndTouchesNothing()
    {
        _r.Service.OnSessionLaunched();

        Assert.AreEqual(1, _r.LiveRecords().Count);
        Assert.AreEqual(0, _r.Adapter.Count("release:") + _r.Adapter.Count("despawn:"));
    }

    [TestMethod]
    public void SessionLaunched_DropsAHostWhosePartyIsGone_AndRestsTheSide()
    {
        _r.Adapter.Parties.Remove(_host.PartyId);

        _r.Service.OnSessionLaunched();

        Assert.AreEqual(0, _r.LiveRecords().Count);
        StringAssert.Contains(_r.Service.CaptureState(), "\"cool\":{\"Evil\":220}");
    }

    [TestMethod]
    public void SessionLaunched_RetiresAHostWhoseSwitchIsOff()
    {
        _r.Settings.EvilHostsEnabled = false;

        _r.Service.OnSessionLaunched();

        Assert.IsTrue(_r.Adapter.Called($"despawn:{_host.PartyId}"));
        Assert.AreEqual(0, _r.LiveRecords().Count);
    }

    [TestMethod]
    public void SessionLaunched_NoHosts_TouchesNothing()
    {
        var rig = new FireAndTributeRig();

        rig.Service.OnSessionLaunched();

        Assert.AreEqual(0, rig.Adapter.Calls.Count);
    }

    // ---- every path out of the record ends with the pin released or the party gone ---------------------------------

    [TestMethod]
    public void EveryEndPath_LeavesNoPinnedPartyBehindUntracked()
    {
        // A tracked host is pinned. After each way out, either the party is gone or its pin was released.
        var paths = new Dictionary<string, Action<FireAndTributeRig, RaidHostRecord>>
        {
            ["coop"] = (r, h) => { r.Coop.IsSessionActive.Returns(true); r.Service.OnHourlyTick(); },
            ["switch off"] = (r, h) => { r.Settings.Enabled = false; r.Service.OnHourlyTick(); },
            ["session launch in coop"] = (r, h) => { r.Coop.ShouldDeferToHost.Returns(true); r.Service.OnSessionLaunched(); },
        };
        foreach (var path in paths)
        {
            var rig = new FireAndTributeRig();
            rig.Random.DoubleFallback = 0.99;
            var host = rig.SpawnEvil();

            path.Value(rig, host);

            bool gone = !rig.Adapter.Parties.ContainsKey(host.PartyId);
            bool released = rig.Adapter.Called("release:" + host.PartyId);
            Assert.IsTrue(gone || released, path.Key + ": the party is neither removed nor unpinned");
            Assert.AreEqual(0, rig.LiveRecords().Count, path.Key);
        }
    }
}
