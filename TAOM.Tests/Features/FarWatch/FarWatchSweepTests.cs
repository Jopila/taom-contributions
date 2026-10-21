using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The report of a surroundings ride (docs/features/far-watch.md "Report"): which parties in sight count, in what order the
/// six are listed, what the scout learns of each (the digit stages, through the real Hidden Hosts service), which keep their
/// names past the horizon, and how a settlement ride breaks down its defenders. Parties sit at the distances of the Hidden Hosts
/// threshold table: 123 men read one digit at 0.5 of the seeing range.
/// </summary>
[TestClass]
public class FarWatchSweepTests
{
    private const double Now = 1000d;
    private const int Hour = 1000;

    private FarWatchStore _store = null!;
    private IFarWatchWorldAdapter _world = null!;
    private IHiddenHostsSettings _hiddenSettings = null!;
    private HiddenHostsService _hidden = null!;
    private FarWatchService _sut = null!;
    private List<ScoutSighting> _seen = null!;

    [TestInitialize]
    public void SetUp()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new FarWatchStore(logger);
        _world = Substitute.For<IFarWatchWorldAdapter>();
        _world.NowHours.Returns(Now);
        _world.IsHeroWithParty("scout_1").Returns(true);
        _hiddenSettings = Substitute.For<IHiddenHostsSettings>();
        _hiddenSettings.Enabled.Returns(true);
        _hidden = new HiddenHostsService(_hiddenSettings, _store);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var camps = Substitute.For<ICampSettingsProvider>();
        camps.Enabled.Returns(true);
        _sut = new FarWatchService(_store, _world, _hidden, coop, Substitute.For<IDedicatedServerProvider>(), camps, logger);

        _seen = new List<ScoutSighting>();
        _world.When(w => w.CollectSightings(Arg.Any<List<ScoutSighting>>())).Do(call =>
        {
            var into = call.Arg<List<ScoutSighting>>();
            into.Clear();
            into.AddRange(_seen);
        });
    }

    private void Ride(int skill = 0) => _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null,
        Now - 8d, 8d, skill);

    /// <summary>A host to the north, <paramref name="ratio"/> of the 100-unit seeing range away, mostly foot.</summary>
    private static ScoutSighting Host(string id, int count, float ratio = 0.5f, float dx = 0f, float dy = 1f) => new()
    {
        Intel = new PartyIntelSnapshot
        {
            HasData = true, StringId = id, HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * 100f, MainSeeingRange = 100f,
            SpottingRatio = 1f, IsVisible = true, NowHour = Hour,
        },
        Name = "Host " + id, Dx = dx * 10f, Dy = dy * 10f, IsLordWithHero = true, Foot = count,
    };

    private static ScoutSighting With(ScoutSighting s, Func<PartyIntelSnapshot, PartyIntelSnapshot> intel) => s with { Intel = intel(s.Intel) };

    private static ScoutSighting Flag(ScoutSighting s, bool villager = false, bool caravan = false, bool attached = false, bool inSettlement = false,
        bool leader = false) => s with
    {
        Intel = s.Intel with { LeadsArmy = leader }, IsVillager = villager, IsCaravan = caravan, IsAttachedToArmy = attached, InSettlement = inSettlement,
    };

    private ScoutReport Sweep(params ScoutSighting[] hosts)
    {
        _seen.Clear();
        _seen.AddRange(hosts);
        Ride();
        return _sut.Complete(Now)!;
    }

    private string? Plate(string id, int count, float ratio) =>
        _hidden.Evaluate(Host(id, count, ratio).Intel with { NowHour = Hour + 1 }).CountText;

    // ---- Who counts

    [DataTestMethod]
    [DataRow("villager")]
    [DataRow("caravan")]
    [DataRow("attached")]
    [DataRow("inSettlement")]
    [DataRow("main")]
    [DataRow("clan")]
    [DataRow("army")]
    [DataRow("hidden")]
    [DataRow("nodata")]
    public void AnExcludedParty_IsNotReported_AndIsNotRemembered_AndTheIncludedOneBesideItIs(string excluded)
    {
        var bad = Host("bad", 123);
        bad = excluded switch
        {
            "villager" => Flag(bad, villager: true),
            "caravan" => Flag(bad, caravan: true),
            "attached" => Flag(bad, attached: true),
            "inSettlement" => Flag(bad, inSettlement: true),
            "main" => With(bad, i => i with { IsMainParty = true }),
            "clan" => With(bad, i => i with { InPlayerClan = true }),
            "army" => With(bad, i => i with { InPlayerArmy = true }),
            "hidden" => With(bad, i => i with { IsInfoHidden = true }),
            _ => With(bad, i => i with { HasData = false }),
        };

        var report = Sweep(bad, Host("good", 123, dy: -1f));

        CollectionAssert.AreEqual(new[] { "Host good" }, report.Hosts.Select(h => h.Name).ToList(), excluded);
        Assert.IsFalse(_store.IsNamedByScout("bad", Hour + 1), excluded + " is not named either");
        Assert.AreEqual("1??", Plate("bad", 123, 0.5f), excluded + " leaves no memory");
    }

    [TestMethod]
    public void AnArmyLeader_IsReported_ItsFollowersAreNot()
    {
        var report = Sweep(Flag(Host("leader", 300), leader: true), Flag(Host("member", 40), attached: true));

        CollectionAssert.AreEqual(new[] { "Host leader" }, report.Hosts.Select(h => h.Name).ToList());
    }

    [TestMethod]
    public void AnArmyMemberNotYetAttached_StillMovesAlone_SoItIsARow()
    {
        var unattached = Host("member", 40, dy: -1f);   // in the army, but IsAttachedToArmy is false until it merges

        var report = Sweep(Flag(Host("leader", 300), leader: true), unattached);

        CollectionAssert.AreEqual(new[] { "Host leader", "Host member" }, report.Hosts.Select(h => h.Name).ToList());
    }

    [TestMethod]
    public void AWeakerScoutsReturn_KeepsTheNamesUntilTheLongerHeldBuffEnds()
    {
        _store.SetBuff(0.5f, Now - 12d, 48d);   // ends its fade at Now - 12 + 48 + 24 = Now + 60

        Sweep(Host("a", 123));

        Assert.IsTrue(_store.IsNamedByScout("a", (int)(Now + 59)));
        Assert.IsFalse(_store.IsNamedByScout("a", (int)(Now + 61)));
    }

    [TestMethod]
    public void NoSightings_ReportsNoHosts_AndNamesNobody_ButTheSightIsStillRaised()
    {
        var report = Sweep();

        Assert.AreEqual(0, report.Hosts.Count);
        Assert.AreEqual(0, _store.NamedCount);
        Assert.AreEqual(24, report.SightHours, "an unskilled scout's sight holds for a day");
    }

    // ---- What the scout learns

    [TestMethod]
    public void OneStage_AddsOneDigit_ToWhatTheDistanceShows()
    {
        var report = Sweep(Host("a", 123, 0.5f));

        Assert.AreEqual("12?", report.Hosts.Single().Count);
    }

    [TestMethod]
    public void ASkilledScout_TwoStages_AddTwoDigits()
    {
        _seen.Clear();
        _seen.Add(Host("a", 123, 0.7f));
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, Now - 6d, 6d, 150);

        var report = _sut.Complete(Now)!;

        Assert.AreEqual("12?", report.Hosts.Single().Count, "none readable at 0.7, two stages give two digits");
    }

    [TestMethod]
    public void AnUnskilledScout_JustUnderTheLimit_AddsOne()
    {
        _seen.Clear();
        _seen.Add(Host("a", 123, 0.7f));
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, Now - 6d, 6d, 149);

        Assert.AreEqual("1??", _sut.Complete(Now)!.Hosts.Single().Count);
    }

    [TestMethod]
    public void TheRaise_NeverPassesTheDigitCount()
    {
        _seen.Add(Host("a", 7, 0.5f));
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, Now - 6d, 6d, 300);

        Assert.AreEqual("7", _sut.Complete(Now)!.Hosts.Single().Count);
    }

    [TestMethod]
    public void TheMemoryRow_HoldsTheRaisedDigits_ReadBackThroughTheNameplate()
    {
        Sweep(Host("a", 123, 0.5f));

        Assert.AreEqual("~12?", Plate("a", 123, 0.5f));
    }

    [TestMethod]
    public void EveryEligibleHostIsRaised_NotOnlyTheSixListed()
    {
        var hosts = Enumerable.Range(1, 8).Select(i => Host("h" + i, 123, 0.5f, dy: i)).ToArray();

        var report = Sweep(hosts);

        Assert.AreEqual(6, report.Hosts.Count);
        for (int i = 1; i <= 8; i++)
            Assert.AreEqual("~12?", Plate("h" + i, 123, 0.5f), "h" + i);
    }

    [TestMethod]
    public void WithTheFogOff_TheReportGivesTheWholeNumber_AndWritesNoMemory()
    {
        _hiddenSettings.Enabled.Returns(false);

        var report = Sweep(Host("a", 123, 0.5f));

        Assert.AreEqual("123", report.Hosts.Single().Count);
        _hiddenSettings.Enabled.Returns(true);
        Assert.AreEqual("1??", Plate("a", 123, 0.5f));
    }

    [TestMethod]
    public void ATrueSightHost_IsReportedWhole()
    {
        var report = Sweep(With(Host("a", 123, 0.9f), i => i with { TrueSight = true }));

        Assert.AreEqual("123", report.Hosts.Single().Count);
    }

    // ---- Which six, in what order

    private static ScoutSighting Other(string id, int count, float ratio) => new()
    {
        Intel = Host(id, count, ratio).Intel, Name = "Others " + id, Dx = 0f, Dy = 5f, IsLordWithHero = false, Foot = count,
    };

    [TestMethod]
    public void TheSix_ArmyLeadersThenLordsThenOthers_NearestFirst()
    {
        var report = Sweep(
            Other("o", 20, 0.1f),
            Host("lordFar", 50, 0.8f),
            Host("lordNear", 50, 0.2f),
            Flag(Host("army", 300, 0.9f), leader: true));

        CollectionAssert.AreEqual(new[] { "Host army", "Host lordNear", "Host lordFar", "Others o" }, report.Hosts.Select(h => h.Name).ToList());
    }

    [TestMethod]
    public void AtTheSameRankAndDistance_TheIdDecides()
    {
        var report = Sweep(Host("b", 50, 0.4f), Host("a", 50, 0.4f));

        CollectionAssert.AreEqual(new[] { "Host a", "Host b" }, report.Hosts.Select(h => h.Name).ToList());
    }

    [TestMethod]
    public void MoreThanSixHosts_ListTheNearestSix_AndNameExactlyThoseSix()
    {
        var hosts = Enumerable.Range(1, 9).Select(i => Host("h" + i, 50, 0.1f * i, dy: 1f)).ToArray();

        var report = Sweep(hosts);

        CollectionAssert.AreEqual(new[] { "Host h1", "Host h2", "Host h3", "Host h4", "Host h5", "Host h6" }, report.Hosts.Select(h => h.Name).ToList());
        Assert.AreEqual(6, _store.NamedCount);
        for (int i = 1; i <= 6; i++) Assert.IsTrue(_store.IsNamedByScout("h" + i, Hour + 1), "h" + i);
        Assert.IsFalse(_store.IsNamedByScout("h7", Hour + 1));
        Assert.IsFalse(_store.IsNamedByScout("h8", Hour + 1));
    }

    [TestMethod]
    public void TheNamedHosts_KeepTheirNamesUntilTheSightEnds_ReturnHourPlusHoldPlusHalfTheHold()
    {
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, Now - 6d, 6d, 150);
        _seen.Add(Host("a", 123, 0.5f));

        _sut.Complete(Now);

        Assert.AreEqual(Now + 54d, _store.NamedUntilHour, 1e-9, "36 hours held plus 18 fading");
        Assert.IsTrue(_store.IsNamedByScout("a", (int)(Now + 53d)));
        Assert.IsFalse(_store.IsNamedByScout("a", (int)(Now + 54d)));
    }

    [TestMethod]
    public void ANewReport_ReplacesTheLastOnesNames()
    {
        Sweep(Host("old", 50));
        Sweep(Host("new", 50));

        Assert.IsFalse(_store.IsNamedByScout("old", Hour + 1));
        Assert.IsTrue(_store.IsNamedByScout("new", Hour + 1));
    }

    [TestMethod]
    public void AHostWithNoDirection_IsDropped_AsARow_ButStillRaised()
    {
        var onTop = Host("onTop", 123, 0.5f);
        onTop = new ScoutSighting { Intel = onTop.Intel, Name = "OnTop", Dx = 0f, Dy = 0f, IsLordWithHero = true, Foot = 123 };

        var report = Sweep(onTop, Host("ok", 123, 0.5f));

        CollectionAssert.AreEqual(new[] { "Host ok" }, report.Hosts.Select(h => h.Name).ToList());
        Assert.IsFalse(_store.IsNamedByScout("onTop", Hour + 1));
        Assert.AreEqual("~12?", Plate("onTop", 123, 0.5f));
    }

    // ---- The row

    [DataTestMethod]
    [DataRow(0f, 1f, 0)]
    [DataRow(1f, 1f, 1)]
    [DataRow(1f, 0f, 2)]
    [DataRow(0f, -1f, 4)]
    [DataRow(-1f, 0f, 6)]
    public void TheRow_NamesTheCompassSector(float dx, float dy, int sector)
    {
        var report = Sweep(Host("a", 50, dx: dx, dy: dy));

        Assert.AreEqual(sector, report.Hosts.Single().Direction);
    }

    [TestMethod]
    public void TheRow_NamesWhatTheHostIsMadeOf()
    {
        var mounted = Host("a", 90);
        var horse = new ScoutSighting
        {
            Intel = mounted.Intel, Name = mounted.Name, Dx = 0f, Dy = 10f, IsLordWithHero = true,
            Foot = 10, Archers = 20, Horse = 60,
        };

        Assert.AreEqual(ScoutComposition.Horse, Sweep(horse).Hosts.Single().Composition);
    }

    [TestMethod]
    public void TheReport_CarriesTheScoutsNameAndKind()
    {
        var report = Sweep(Host("a", 50));

        Assert.AreEqual(ScoutKind.Surroundings, report.Kind);
        Assert.AreEqual("Mirien", report.ScoutName);
        Assert.IsFalse(report.Lost);
    }

    // ---- A settlement ride

    private static ScoutSiteReport Site(bool ownSide = false, params ScoutDefender[] defenders) => new()
    {
        Found = true, Name = "Edoras",
        Intel = new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_9", DefenderCount = defenders.Sum(d => d.Count), NowHour = Hour, MainSeeingRange = 100f,
            DistanceToMain = 70f, OwnedByPlayerClan = ownSide,
        },
        Defenders = defenders.ToList(),
    };

    [TestMethod]
    public void ASettlementRide_BreaksTheDefendersDownByParty_AndKeepsThePartyKinds()
    {
        _store.StartRide(ScoutKind.Settlement, "scout_1", "Mirien", false, "town_9", "Edoras", Now - 4d, 4.2d, 150);
        _world.ReadSite("town_9").Returns(Site(false,
            new ScoutDefender { Kind = ScoutDefenderKind.Garrison, Name = "Edoras Garrison", Count = 120, Foot = 70, Archers = 50 },
            new ScoutDefender { Kind = ScoutDefenderKind.Militia, Name = "Edoras Militia", Count = 40, Foot = 40 },
            new ScoutDefender { Kind = ScoutDefenderKind.Lord, Name = "Eomer's Party", Count = 85, Horse = 85 }));

        var report = _sut.Complete(Now)!;

        Assert.AreEqual("Edoras", report.SiteName);
        Assert.AreEqual(245, report.SiteTotal);
        CollectionAssert.AreEqual(
            new[] { ScoutDefenderKind.Garrison, ScoutDefenderKind.Militia, ScoutDefenderKind.Lord },
            report.Defenders.Select(d => d.Kind).ToList());
        Assert.AreEqual(ScoutComposition.Foot, report.Defenders[0].Composition);
        Assert.AreEqual(ScoutComposition.Horse, report.Defenders[2].Composition);
        Assert.AreEqual(85, report.Defenders[2].Count);
        Assert.AreEqual(0, report.Hosts.Count);
        _world.DidNotReceiveWithAnyArgs().CollectSightings(default!);
    }

    [TestMethod]
    public void ASettlementRide_TheOwnSideIsNotRemembered()
    {
        _store.StartRide(ScoutKind.Settlement, "scout_1", "Mirien", false, "town_9", "Edoras", Now - 4d, 4.2d, 150);
        _world.ReadSite("town_9").Returns(Site(true, new ScoutDefender { Kind = ScoutDefenderKind.Garrison, Name = "G", Count = 245, Foot = 245 }));

        _sut.Complete(Now);

        Assert.IsNull(_hidden.EvaluateTooltipSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_9", DefenderCount = 245, NowHour = Hour + 2, MainSeeingRange = 100f, DistanceToMain = 90f,
        }).MemoryText);
    }
}
