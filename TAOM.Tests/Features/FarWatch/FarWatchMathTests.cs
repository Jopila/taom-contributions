using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FarWatch;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The arithmetic of Far Watch (docs/features/far-watch.md "Constants"): how long a ride takes, the sight it earns and how
/// that sight holds and fades, the digits and XP it pays, the compass, the composition hint, the order of the report and the
/// pick of settlements. Hours are doubles, factors floats, and every gate is a positive requirement so a NaN fails it.
/// </summary>
[TestClass]
public class FarWatchMathTests
{
    // ---- Ride length

    [DataTestMethod]
    [DataRow(0, 8d)]
    [DataRow(150, 6d)]
    [DataRow(300, 4d)]
    [DataRow(600, 4d)]
    [DataRow(-40, 8d)]
    public void RideHours_Surroundings_EightLessFourTimesSkillOverThreeHundred(int skill, double expected) =>
        Assert.AreEqual(expected, FarWatchMath.RideHours(ScoutKind.Surroundings, skill), 1e-9);

    [DataTestMethod]
    [DataRow(0, 5.6d)]
    [DataRow(150, 4.2d)]
    [DataRow(300, 2.8d)]
    [DataRow(1000, 2.8d)]
    public void RideHours_Settlement_IsSeventyPercentOfThat(int skill, double expected) =>
        Assert.AreEqual(expected, FarWatchMath.RideHours(ScoutKind.Settlement, skill), 1e-9);

    [TestMethod]
    public void RideHours_ForNoKind_IsZero() => Assert.AreEqual(0d, FarWatchMath.RideHours(ScoutKind.None, 100));

    // ---- How far along a ride is

    [DataTestMethod]
    [DataRow(10d, 6d, 10d, 0f)]
    [DataRow(10d, 6d, 13d, 0.5f)]
    [DataRow(10d, 6d, 16d, 1f)]
    [DataRow(10d, 6d, 90d, 1f)]
    [DataRow(10d, 6d, 9d, 0f)]
    public void Fraction_IsZeroToOneAlongTheRide(double start, double duration, double now, float expected) =>
        Assert.AreEqual(expected, FarWatchMath.Fraction(start, duration, now), 1e-6f);

    [DataTestMethod]
    [DataRow(double.NaN, 6d, 12d)]
    [DataRow(10d, double.NaN, 12d)]
    [DataRow(10d, 6d, double.NaN)]
    [DataRow(10d, 6d, double.PositiveInfinity)]
    [DataRow(10d, 0d, 12d)]
    [DataRow(10d, -3d, 12d)]
    public void Fraction_AnyUnusableInput_IsZero_SoABadClockNeverCompletesARide(double start, double duration, double now) =>
        Assert.AreEqual(0f, FarWatchMath.Fraction(start, duration, now));

    // ---- The ramp while a surroundings ride runs

    [DataTestMethod]
    [DataRow(10d, 6d, 10d, 0f)]
    [DataRow(10d, 6d, 13d, 0.25f)]
    [DataRow(10d, 6d, 16d, 0.5f)]
    [DataRow(10d, 6d, 40d, 0.5f)]
    [DataRow(10d, 6d, 9d, 0f)]
    public void Ramp_RisesLinearlyToHalf_AndHoldsThere(double start, double duration, double now, float expected) =>
        Assert.AreEqual(expected, FarWatchMath.Ramp(start, duration, now), 1e-6f);

    [DataTestMethod]
    [DataRow(double.NaN, 6d, 12d)]
    [DataRow(10d, double.NaN, 12d)]
    [DataRow(10d, 6d, double.NaN)]
    [DataRow(10d, 6d, double.PositiveInfinity)]
    [DataRow(10d, 0d, 12d)]
    [DataRow(10d, -3d, 12d)]
    public void Ramp_AnyUnusableInput_IsZero(double start, double duration, double now) =>
        Assert.AreEqual(0f, FarWatchMath.Ramp(start, duration, now));

    // ---- The kept sight: full strength for a hold, then a linear taper over half of it

    [DataTestMethod]
    [DataRow(0, 24d)]
    [DataRow(150, 36d)]
    [DataRow(300, 48d)]
    [DataRow(900, 48d)]
    [DataRow(-5, 24d)]
    public void HoldHours_TwentyFourWidenedByScouting(int skill, double expected) =>
        Assert.AreEqual(expected, FarWatchMath.HoldHours(skill), 1e-9);

    [DataTestMethod]
    [DataRow(100d, 0.5f)]
    [DataRow(110d, 0.5f)]
    [DataRow(124d, 0.5f)]
    [DataRow(130d, 0.25f)]
    [DataRow(136d, 0f)]
    [DataRow(200d, 0f)]
    [DataRow(99d, 0f)]
    public void BuffAt_HoldsThenTapersToZero_ForAHoldOf24(double now, float expected) =>
        Assert.AreEqual(expected, FarWatchMath.BuffAt(0.5f, 100d, 24d, now), 1e-5f);

    [TestMethod]
    public void BuffAt_AtTheExactEdgeOfTheHold_IsStillFull() =>
        Assert.AreEqual(0.5f, FarWatchMath.BuffAt(0.5f, 100d, 24d, 124d));

    [TestMethod]
    public void BuffAt_ALowerLevel_TapersFromThatLevel() =>
        Assert.AreEqual(0.1f, FarWatchMath.BuffAt(0.2f, 0d, 24d, 30d), 1e-6f);

    [DataTestMethod]
    [DataRow(float.NaN, 100d, 24d, 110d)]
    [DataRow(0f, 100d, 24d, 110d)]
    [DataRow(-0.2f, 100d, 24d, 110d)]
    [DataRow(0.5f, double.NaN, 24d, 110d)]
    [DataRow(0.5f, 100d, double.NaN, 110d)]
    [DataRow(0.5f, 100d, 0d, 110d)]
    [DataRow(0.5f, 100d, 24d, double.NaN)]
    [DataRow(0.5f, 100d, 24d, double.PositiveInfinity)]
    public void BuffAt_AnyUnusableInput_IsZero(float level, double start, double hold, double now) =>
        Assert.AreEqual(0f, FarWatchMath.BuffAt(level, start, hold, now));

    // ---- Digits and XP

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(149, 1)]
    [DataRow(150, 2)]
    [DataRow(300, 2)]
    [DataRow(-20, 1)]
    public void Stages_OneDigitStage_TwoFromScoutingOneHundredFifty(int skill, int expected) =>
        Assert.AreEqual(expected, FarWatchMath.Stages(skill));

    [DataTestMethod]
    [DataRow(0, 150f)]
    [DataRow(100, 200f)]
    [DataRow(300, 300f)]
    [DataRow(-50, 150f)]
    public void Xp_OneHundredFiftyPlusHalfTheSkill(int skill, float expected) =>
        Assert.AreEqual(expected, FarWatchMath.Xp(skill));

    [TestMethod]
    public void Xp_ASkillPastTheSaneRange_IsClamped() => Assert.AreEqual(650f, FarWatchMath.Xp(5000));

    // ---- Compass: +Y is north, +X east, eight 45 degree sectors

    [DataTestMethod]
    [DataRow(0f, 10f, 0)]
    [DataRow(10f, 10f, 1)]
    [DataRow(10f, 0f, 2)]
    [DataRow(10f, -10f, 3)]
    [DataRow(0f, -10f, 4)]
    [DataRow(-10f, -10f, 5)]
    [DataRow(-10f, 0f, 6)]
    [DataRow(-10f, 10f, 7)]
    public void CompassSector_AtEachSectorCentre(float dx, float dy, int expected) =>
        Assert.AreEqual(expected, FarWatchMath.CompassSector(dx, dy));

    [DataTestMethod]
    [DataRow(22.4, 0)]
    [DataRow(22.6, 1)]
    [DataRow(67.4, 1)]
    [DataRow(67.6, 2)]
    [DataRow(112.4, 2)]
    [DataRow(112.6, 3)]
    [DataRow(157.4, 3)]
    [DataRow(157.6, 4)]
    [DataRow(202.4, 4)]
    [DataRow(202.6, 5)]
    [DataRow(247.4, 5)]
    [DataRow(247.6, 6)]
    [DataRow(292.4, 6)]
    [DataRow(292.6, 7)]
    [DataRow(337.4, 7)]
    [DataRow(337.6, 0)]
    public void CompassSector_JustEitherSideOfEachBoundary(double degreesFromNorth, int expected)
    {
        double r = degreesFromNorth * System.Math.PI / 180d;
        Assert.AreEqual(expected, FarWatchMath.CompassSector((float)(100d * System.Math.Sin(r)), (float)(100d * System.Math.Cos(r))));
    }

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(float.NaN, 5f)]
    [DataRow(5f, float.NaN)]
    [DataRow(float.PositiveInfinity, 5f)]
    [DataRow(5f, float.NegativeInfinity)]
    public void CompassSector_ZeroOrNonFiniteVector_HasNoDirection(float dx, float dy) =>
        Assert.AreEqual(-1, FarWatchMath.CompassSector(dx, dy));

    // ---- What a host is made of

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(5, 0)]
    [DataRow(1, 1)]
    [DataRow(4, 1)]
    [DataRow(2, 2)]
    [DataRow(6, 2)]
    [DataRow(7, 2)]
    [DataRow(3, 3)]
    [DataRow(8, -1)]
    [DataRow(99, -1)]
    [DataRow(-1, -1)]
    public void BucketOf_FoldsTheEightFormationClassesIntoFourKinds(int formationClass, int bucket) =>
        Assert.AreEqual(bucket, FarWatchMath.BucketOf(formationClass));

    [DataTestMethod]
    [DataRow(9, 3, 3, 3, "Foot")]
    [DataRow(3, 9, 3, 3, "Archers")]
    [DataRow(3, 3, 9, 3, "Horse")]
    [DataRow(3, 3, 3, 9, "HorseArchers")]
    public void Composition_TheKindWithTheMostMen(int foot, int archers, int horse, int horseArchers, string expected) =>
        Assert.AreEqual(expected, FarWatchMath.Composition(foot, archers, horse, horseArchers).ToString());

    [DataTestMethod]
    [DataRow(5, 5, 1, 1, "Foot")]
    [DataRow(1, 5, 5, 1, "Archers")]
    [DataRow(1, 1, 5, 5, "Horse")]
    [DataRow(5, 1, 1, 5, "Foot")]
    [DataRow(4, 4, 4, 4, "Foot")]
    public void Composition_TiesGoToFootThenArchersThenHorseThenHorseArchers(int foot, int archers, int horse, int horseArchers, string expected) =>
        Assert.AreEqual(expected, FarWatchMath.Composition(foot, archers, horse, horseArchers).ToString());

    [TestMethod]
    public void Composition_NoMen_HasNoHint()
    {
        Assert.AreEqual(ScoutComposition.None, FarWatchMath.Composition(0, 0, 0, 0));
        Assert.AreEqual(ScoutComposition.None, FarWatchMath.Composition(-3, 0, 0, 0));
    }

    // ---- The kinds of an army and the name of its plate

    [TestMethod]
    public void AddMen_ALeaderAllFootAndAnAttachedPartyAllHorseArchers_GivesTheArmysMakeUp()
    {
        int foot = 0, archers = 0, horse = 0, horseArchers = 0;

        FarWatchMath.AddMen(0, 60, ref foot, ref archers, ref horse, ref horseArchers);   // the leader's own roster: Infantry
        FarWatchMath.AddMen(3, 90, ref foot, ref archers, ref horse, ref horseArchers);   // an attached party: HorseArcher
        FarWatchMath.AddMen(3, 30, ref foot, ref archers, ref horse, ref horseArchers);

        Assert.AreEqual((60, 0, 0, 120), (foot, archers, horse, horseArchers), "adds to what is there, never replaces it");
        Assert.AreEqual(ScoutComposition.HorseArchers, FarWatchMath.Composition(foot, archers, horse, horseArchers), "they dominate");
    }

    [TestMethod]
    public void AddMen_TheLeaderAloneWouldHaveReadFoot()
    {
        int foot = 0, archers = 0, horse = 0, horseArchers = 0;

        FarWatchMath.AddMen(0, 60, ref foot, ref archers, ref horse, ref horseArchers);

        Assert.AreEqual(ScoutComposition.Foot, FarWatchMath.Composition(foot, archers, horse, horseArchers));
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(5, 0)]
    [DataRow(1, 1)]
    [DataRow(4, 1)]
    [DataRow(2, 2)]
    [DataRow(6, 2)]
    [DataRow(7, 2)]
    [DataRow(3, 3)]
    public void AddMen_EachFormationClassGoesToItsKind(int formationClass, int kind)
    {
        int[] counts = new int[4];
        int foot = 0, archers = 0, horse = 0, horseArchers = 0;

        FarWatchMath.AddMen(formationClass, 7, ref foot, ref archers, ref horse, ref horseArchers);
        counts[0] = foot; counts[1] = archers; counts[2] = horse; counts[3] = horseArchers;

        Assert.AreEqual(7, counts[kind]);
        Assert.AreEqual(7, counts.Sum());
    }

    [DataTestMethod]
    [DataRow(8, 5)]
    [DataRow(-1, 5)]
    [DataRow(0, 0)]
    [DataRow(0, -3)]
    public void AddMen_AnUnmappedClassOrNoMen_AddsNothing(int formationClass, int healthy)
    {
        int foot = 1, archers = 2, horse = 3, horseArchers = 4;

        FarWatchMath.AddMen(formationClass, healthy, ref foot, ref archers, ref horse, ref horseArchers);

        Assert.AreEqual((1, 2, 3, 4), (foot, archers, horse, horseArchers));
    }

    [TestMethod]
    public void PlateName_AnArmyGroupShowsTheArmysName_ElseTheLeadersElseThePartys()
    {
        Assert.AreEqual("Host of Rohan", FarWatchMath.PlateName(true, "Host of Rohan", "Eomer", "Eomer's party"));
        Assert.AreEqual("Eomer", FarWatchMath.PlateName(false, "Host of Rohan", "Eomer", "Eomer's party"), "an army member not yet attached is a plain lord party");
        Assert.AreEqual("Eomer's party", FarWatchMath.PlateName(false, null, null, "Eomer's party"));
        Assert.AreEqual("Eomer", FarWatchMath.PlateName(true, "", "Eomer", "Eomer's party"), "an army group with no army name falls through");
        Assert.AreEqual("", FarWatchMath.PlateName(false, null, null, null));
    }

    // ---- The order of the report

    private static ScoutSighting S(string id, float distance, bool armyLeader = false, bool lordWithHero = false) => new()
    {
        Intel = new PartyIntelSnapshot { HasData = true, StringId = id, DistanceToMain = distance, LeadsArmy = armyLeader },
        Name = id, IsLordWithHero = lordWithHero,
    };

    private static List<string> Ordered(params ScoutSighting[] all)
    {
        var list = all.ToList();
        list.Sort(FarWatchMath.CompareSightings);
        return list.Select(s => s.Intel.StringId!).ToList();
    }

    [TestMethod]
    public void CompareSightings_AnArmyLeaderBeatsANearerLord() =>
        CollectionAssert.AreEqual(new[] { "army", "lord" }, Ordered(S("lord", 1f, lordWithHero: true), S("army", 30f, armyLeader: true, lordWithHero: true)));

    [TestMethod]
    public void CompareSightings_ALordBeatsANearerOther() =>
        CollectionAssert.AreEqual(new[] { "lord", "other" }, Ordered(S("other", 1f), S("lord", 30f, lordWithHero: true)));

    [TestMethod]
    public void CompareSightings_WithinARank_NearestFirst() =>
        CollectionAssert.AreEqual(new[] { "b", "a" }, Ordered(S("a", 9f), S("b", 4f)));

    [TestMethod]
    public void CompareSightings_AtTheSameDistance_ById() =>
        CollectionAssert.AreEqual(new[] { "a", "b" }, Ordered(S("b", 4f), S("a", 4f)));

    [TestMethod]
    public void CompareSightings_ANonFiniteDistance_GoesLastWithinItsRank() =>
        CollectionAssert.AreEqual(new[] { "near", "bad" }, Ordered(S("bad", float.NaN), S("near", 99f)));

    // ---- Which settlements the pick offers

    private static ScoutSite Site(string id, float distance, bool own = false) =>
        new() { Id = id, Name = id, Distance = distance, OwnedByPlayerClan = own };

    private static List<string> Pick(params ScoutSite[] all)
    {
        var into = new List<ScoutSite>();
        FarWatchMath.PickSites(all, into);
        return into.Select(s => s.Id).ToList();
    }

    [TestMethod]
    public void PickSites_FiftyIsIn_JustPastFiftyIsOut() =>
        CollectionAssert.AreEqual(new[] { "in" }, Pick(Site("in", 50f), Site("out", 50.01f)));

    [TestMethod]
    public void PickSites_TheClansOwnFiefsAreLeftOut() =>
        CollectionAssert.AreEqual(new[] { "foreign" }, Pick(Site("own", 5f, own: true), Site("foreign", 9f)));

    [TestMethod]
    public void PickSites_NineCandidates_GiveTheNearestEight_NearestFirst()
    {
        var sites = Enumerable.Range(1, 9).Select(i => Site("s" + i, 10f + (10 - i))).ToArray();

        var picked = Pick(sites);

        Assert.AreEqual(FarWatchMath.MaxSites, picked.Count);
        Assert.AreEqual("s9", picked[0]);
        CollectionAssert.DoesNotContain(picked, "s1", "the farthest is the one dropped");
    }

    [TestMethod]
    public void PickSites_EqualDistances_BreakTheTieById() =>
        CollectionAssert.AreEqual(new[] { "a", "b" }, Pick(Site("b", 7f), Site("a", 7f)));

    [TestMethod]
    public void PickSites_ANonFiniteOrNegativeDistance_IsDropped() =>
        CollectionAssert.AreEqual(new[] { "ok" },
            Pick(Site("nan", float.NaN), Site("inf", float.PositiveInfinity), Site("neg", -1f), Site("ok", 3f)));

    [TestMethod]
    public void PickSites_NothingInRange_FillsNothing_AndClearsWhatWasThere()
    {
        var into = new List<ScoutSite> { Site("stale", 1f) };

        FarWatchMath.PickSites(new[] { Site("far", 90f) }, into);

        Assert.AreEqual(0, into.Count);
    }
}
