using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FarWatch;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The one ride, the kept sight, the rest and the named hosts (docs/features/far-watch.md "Data and save"), as plain values,
/// and the validated record that carries them through a save. A bad record is dropped part by part with one warning and
/// never throws: a throw out of a load handler loops the loading screen.
/// </summary>
[TestClass]
public class FarWatchStoreTests
{
    private IModLogger _logger = null!;
    private FarWatchStore _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new FarWatchStore(_logger);
    }

    private static FarWatchRecord FullRecord() => new()
    {
        Kind = (int)ScoutKind.Settlement, HeroId = "hero_9", HeroName = "Mirien", SelfScout = true,
        TargetId = "town_3", TargetName = "Edoras", StartHour = 4321.5, DurationHours = 4.2, Skill = 150,
        BuffLevel = 0.4f, BuffStartHour = 4000.25, BuffHoldHours = 36, RestUntilHour = 4400.5, RestRiderName = "Tamsin",
        NamedIds = new List<string> { "p1", "p2", "p3" }, NamedUntilHour = 4500.75,
    };

    [TestMethod]
    public void ANewStore_HoldsNothing()
    {
        Assert.AreEqual(ScoutKind.None, _sut.Kind);
        Assert.IsFalse(_sut.HasRide);
        Assert.IsFalse(_sut.IsScoutAway);
        Assert.AreEqual(0, _sut.NamedCount);
        Assert.AreEqual(0f, _sut.BuffLevel);
        Assert.AreEqual(0d, _sut.RestUntilHour);
    }

    [TestMethod]
    public void StartRide_StoresEveryField()
    {
        _sut.StartRide(ScoutKind.Surroundings, "hero_1", "Aldric", false, null, null, 100.5, 6d, 150);

        Assert.IsTrue(_sut.HasRide);
        Assert.AreEqual(ScoutKind.Surroundings, _sut.Kind);
        Assert.AreEqual("hero_1", _sut.HeroId);
        Assert.AreEqual("Aldric", _sut.HeroName);
        Assert.IsFalse(_sut.SelfScout);
        Assert.AreEqual("", _sut.TargetId);
        Assert.AreEqual(100.5, _sut.StartHour);
        Assert.AreEqual(6d, _sut.DurationHours);
        Assert.AreEqual(150, _sut.Skill);
    }

    [TestMethod]
    public void IsScoutAway_OnlyForASettlementRide()
    {
        _sut.StartRide(ScoutKind.Surroundings, "h", "H", false, null, null, 1d, 6d, 0);
        Assert.IsFalse(_sut.IsScoutAway);

        _sut.StartRide(ScoutKind.Settlement, "h", "H", false, "town_1", "Minas Tirith", 1d, 4d, 0);
        Assert.IsTrue(_sut.IsScoutAway);

        _sut.ClearRide();
        Assert.IsFalse(_sut.IsScoutAway);
    }

    [TestMethod]
    public void ClearRide_KeepsTheBuffTheRestAndTheNamedHosts()
    {
        _sut.StartRide(ScoutKind.Surroundings, "h", "H", true, null, null, 1d, 6d, 10);
        _sut.SetBuff(0.5f, 7d, 24d);
        _sut.SetRest(15d);
        _sut.SetNamed(new[] { "p1" }, 99d);

        _sut.ClearRide();

        Assert.IsFalse(_sut.HasRide);
        Assert.AreEqual("", _sut.HeroId);
        Assert.IsFalse(_sut.SelfScout);
        Assert.AreEqual(0d, _sut.DurationHours);
        Assert.AreEqual(0.5f, _sut.BuffLevel);
        Assert.AreEqual(15d, _sut.RestUntilHour);
        Assert.AreEqual(1, _sut.NamedCount);
    }

    [TestMethod]
    public void SetNamed_KeepsAtMostSix_AndDropsBlankIds()
    {
        _sut.SetNamed(new[] { "a", "", "b", "c", "d", "e", "f", "g", "h" }, 50d);

        Assert.AreEqual(FarWatchStore.MaxNamed, _sut.NamedCount);
        Assert.IsTrue(_sut.IsNamedByScout("f", 10));
        Assert.IsFalse(_sut.IsNamedByScout("g", 10), "the seventh is not kept");
    }

    [TestMethod]
    public void IsNamedByScout_UntilTheHourItEnds()
    {
        _sut.SetNamed(new[] { "p1" }, 100d);

        Assert.IsTrue(_sut.IsNamedByScout("p1", 99));
        Assert.IsFalse(_sut.IsNamedByScout("p1", 100), "the hour the sight ends it ends");
        Assert.IsFalse(_sut.IsNamedByScout("p1", 101));
    }

    [TestMethod]
    public void IsNamedByScout_OtherIdsNullAndNoClock_AreNotNamed()
    {
        _sut.SetNamed(new[] { "p1" }, 100d);

        Assert.IsFalse(_sut.IsNamedByScout("p2", 10));
        Assert.IsFalse(_sut.IsNamedByScout("P1", 10), "ids compare as written");
        Assert.IsFalse(_sut.IsNamedByScout(null!, 10));
        Assert.IsFalse(_sut.IsNamedByScout("", 10));
        Assert.IsFalse(_sut.IsNamedByScout("p1", -1), "no clock answers false");
    }

    [TestMethod]
    public void SetNamed_AReplacesTheLastList()
    {
        _sut.SetNamed(new[] { "p1", "p2" }, 100d);
        _sut.SetNamed(new[] { "p3" }, 200d);

        Assert.IsFalse(_sut.IsNamedByScout("p1", 10));
        Assert.IsTrue(_sut.IsNamedByScout("p3", 150));
        Assert.AreEqual(200d, _sut.NamedUntilHour);
    }

    [TestMethod]
    public void ResetForNewSession_ResetsEverything()
    {
        _sut.LoadFrom(FullRecord());

        _sut.ResetForNewSession();

        Assert.AreEqual(ScoutKind.None, _sut.Kind);
        Assert.AreEqual("", _sut.HeroName);
        Assert.AreEqual(0f, _sut.BuffLevel);
        Assert.AreEqual(0d, _sut.BuffHoldHours);
        Assert.AreEqual(0d, _sut.RestUntilHour);
        Assert.AreEqual(0, _sut.NamedCount);
        Assert.AreEqual(0d, _sut.NamedUntilHour);
        Assert.IsFalse(_sut.IsNamedByScout("p1", 1));
    }

    // ---- The save record

    [TestMethod]
    public void SaveThenLoad_IntoAFreshStore_RoundTripsEveryField()
    {
        _sut.LoadFrom(FullRecord());

        var fresh = new FarWatchStore(_logger);
        fresh.LoadFrom(_sut.SaveInto());

        var r = fresh.SaveInto();
        Assert.AreEqual((int)ScoutKind.Settlement, r.Kind);
        Assert.AreEqual("hero_9", r.HeroId);
        Assert.AreEqual("Mirien", r.HeroName);
        Assert.IsTrue(r.SelfScout);
        Assert.AreEqual("town_3", r.TargetId);
        Assert.AreEqual("Edoras", r.TargetName);
        Assert.AreEqual(4321.5, r.StartHour);
        Assert.AreEqual(4.2, r.DurationHours);
        Assert.AreEqual(150, r.Skill);
        Assert.AreEqual(0.4f, r.BuffLevel);
        Assert.AreEqual(4000.25, r.BuffStartHour);
        Assert.AreEqual(36d, r.BuffHoldHours);
        Assert.AreEqual(4400.5, r.RestUntilHour);
        Assert.AreEqual("Tamsin", r.RestRiderName);
        CollectionAssert.AreEqual(new[] { "p1", "p2", "p3" }, r.NamedIds);
        Assert.AreEqual(4500.75, r.NamedUntilHour);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void SaveInto_OfAnEmptyStore_IsTheDefaultRecord_AndChangesNothing()
    {
        var r = _sut.SaveInto();

        Assert.AreEqual(0, r.Kind);
        Assert.IsTrue(string.IsNullOrEmpty(r.HeroId));
        Assert.AreEqual(0, r.NamedIds?.Count ?? 0);
        Assert.IsFalse(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_ADefaultRecord_ASaveFromBeforeTheFeature_LoadsAsNothing()
    {
        _sut.StartRide(ScoutKind.Surroundings, "h", "H", false, null, null, 1d, 6d, 0);
        _sut.SetBuff(0.5f, 1d, 24d);

        _sut.LoadFrom(new FarWatchRecord());

        Assert.IsFalse(_sut.HasRide);
        Assert.AreEqual(0f, _sut.BuffLevel);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void LoadFrom_ReplacesWhatTheLastCampaignLeft()
    {
        _sut.LoadFrom(FullRecord());
        var other = new FarWatchRecord { RestUntilHour = 7d };

        _sut.LoadFrom(other);

        Assert.IsFalse(_sut.HasRide);
        Assert.AreEqual(0, _sut.NamedCount);
        Assert.AreEqual(7d, _sut.RestUntilHour);
    }

    private FarWatchRecord Load(System.Action<FarWatchRecord> breakIt)
    {
        var record = FullRecord();
        breakIt(record);
        _sut.LoadFrom(record);
        return _sut.SaveInto();
    }

    [TestMethod]
    public void LoadFrom_AnUnknownKind_DropsTheRide_KeepsTheRest()
    {
        var r = Load(x => x.Kind = 3);

        Assert.AreEqual(0, r.Kind);
        Assert.IsFalse(_sut.HasRide);
        Assert.AreEqual(0.4f, r.BuffLevel);
        Assert.AreEqual(4400.5, r.RestUntilHour);
        Assert.AreEqual(3, r.NamedIds!.Count);
    }

    [TestMethod]
    public void LoadFrom_ANegativeKind_DropsTheRide()
    {
        Load(x => x.Kind = -1);
        Assert.IsFalse(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_ARideWithNoHero_DropsTheRide()
    {
        Load(x => x.HeroId = "");
        Assert.IsFalse(_sut.HasRide);

        Load(x => x.HeroId = null);
        Assert.IsFalse(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_ASettlementRideWithNoTarget_DropsTheRide()
    {
        Load(x => x.TargetId = "");
        Assert.IsFalse(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_ASurroundingsRideNeedsNoTarget()
    {
        Load(x => { x.Kind = (int)ScoutKind.Surroundings; x.TargetId = null; x.TargetName = null; });

        Assert.IsTrue(_sut.HasRide);
        Assert.AreEqual("", _sut.TargetId);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-1d)]
    public void LoadFrom_AStartThatIsNotAFiniteHour_DropsTheRide(double start)
    {
        Load(x => x.StartHour = start);
        Assert.IsFalse(_sut.HasRide);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(0d)]
    [DataRow(-2d)]
    [DataRow(8.0001d)]
    [DataRow(double.PositiveInfinity)]
    public void LoadFrom_ALengthOutsideZeroToEight_DropsTheRide(double hours)
    {
        Load(x => x.DurationHours = hours);
        Assert.IsFalse(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_AnEightHourRide_IsKept()
    {
        Load(x => x.DurationHours = 8d);
        Assert.IsTrue(_sut.HasRide);
    }

    [TestMethod]
    public void LoadFrom_TheSkill_IsClampedToZeroThroughAThousand()
    {
        Assert.AreEqual(1000, Load(x => x.Skill = 5000).Skill);
        Assert.AreEqual(0, Load(x => x.Skill = -3).Skill);
        Assert.AreEqual(1000, Load(x => x.Skill = 1000).Skill);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-0.1f)]
    [DataRow(0.5001f)]
    [DataRow(float.PositiveInfinity)]
    public void LoadFrom_ABuffLevelOutsideZeroToHalf_DropsTheBuff_KeepsTheRide(float level)
    {
        var r = Load(x => x.BuffLevel = level);

        Assert.AreEqual(0f, r.BuffLevel);
        Assert.AreEqual(0d, r.BuffHoldHours);
        Assert.IsTrue(_sut.HasRide);
    }

    [DataTestMethod]
    [DataRow(double.NaN, 36d)]
    [DataRow(-1d, 36d)]
    [DataRow(10d, double.NaN)]
    [DataRow(10d, -1d)]
    [DataRow(10d, 48.0001d)]
    [DataRow(10d, double.PositiveInfinity)]
    public void LoadFrom_ABuffWithABadStartOrHold_DropsTheBuff(double start, double hold)
    {
        var r = Load(x => { x.BuffStartHour = start; x.BuffHoldHours = hold; });

        Assert.AreEqual(0f, r.BuffLevel);
        Assert.AreEqual(0d, r.BuffStartHour);
        Assert.AreEqual(0d, r.BuffHoldHours);
    }

    [TestMethod]
    public void LoadFrom_AFortyEightHourHold_IsKept() =>
        Assert.AreEqual(48d, Load(x => x.BuffHoldHours = 48d).BuffHoldHours);

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-5d)]
    public void LoadFrom_ARestThatIsNotAFiniteHour_BecomesNoRest(double rest)
    {
        var store = Load(x => { x.RestUntilHour = rest; x.RestRiderName = "Tamsin"; });

        Assert.AreEqual(0d, store.RestUntilHour);
        Assert.AreEqual("", store.RestRiderName, "no rest, no rider to name");
    }

    [TestMethod]
    public void LoadFrom_NamedIds_KeepNonEmptyEntries_AtMostSix()
    {
        var r = Load(x => x.NamedIds = new List<string> { "a", "", "b", null!, "c", "d", "e", "f", "g" });

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f" }, r.NamedIds);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-1d)]
    public void LoadFrom_NamedIdsWithABadUntilHour_AreDropped(double until)
    {
        var r = Load(x => x.NamedUntilHour = until);

        Assert.AreEqual(0, r.NamedIds?.Count ?? 0);
        Assert.AreEqual(0d, r.NamedUntilHour);
    }

    [TestMethod]
    public void LoadFrom_AValidRecord_WarnsNever_AndSeveralBadParts_WarnOnce()
    {
        _sut.LoadFrom(FullRecord());
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);

        Load(x => { x.Kind = 9; x.BuffLevel = float.NaN; x.NamedUntilHour = double.NaN; x.RestUntilHour = double.NaN; });

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void LoadFrom_NullNames_BecomeEmptyStrings()
    {
        Load(x => { x.HeroName = null; x.TargetName = null; });

        Assert.AreEqual("", _sut.HeroName);
        Assert.AreEqual("", _sut.TargetName);
    }

    [TestMethod]
    public void TheStore_ImplementsTheSeamHiddenHostsReads() =>
        Assert.IsInstanceOfType(_sut, typeof(IScoutIntelQuery));

    [TestMethod]
    public void TheStore_HoldsNoReferenceTypeFromTheEngine()
    {
        // A process singleton: an engine object here would keep a finished campaign alive (PartyIntelAdapterTests shape).
        var offenders = typeof(FarWatchStore)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Where(f => !f.FieldType.IsValueType && f.FieldType.Assembly.GetName().Name!.StartsWith("TaleWorlds")
                        || f.FieldType.Namespace != null && f.FieldType.Namespace.StartsWith("TaleWorlds")
                        || f.FieldType.Namespace == "SandBox")
            .Select(f => f.Name)
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }
}
