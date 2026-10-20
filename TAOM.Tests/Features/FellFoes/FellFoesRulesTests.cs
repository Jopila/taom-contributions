using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FellFoes;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Tests.Features.FellFoes;

/// <summary>The three Fell Foes rule passes over a scan snapshot: no engine types, no adapter.</summary>
[TestClass]
public class FellFoesRulesTests
{
    private FellFoesTunables _t = null!;
    private FellFoesMissionState _s = null!;

    [TestInitialize]
    public void Setup()
    {
        _t = FellFoesTunables.Create();
        _s = new FellFoesMissionState();
    }

    private static EnemyAgentView Router(int foes = 2, float health = 1f, float x = 0f, float y = 0f) => new EnemyAgentView
    {
        Handle = new object(), IsPanicked = true, IsFleeing = true, HealthFraction = health, NearbyFoes = foes, Morale = 0.5f,
        DistanceToPlayer = float.NaN, X = x, Y = y,
    };

    private static EnemyAgentView Archer(float distance = 20f, float morale = 80f) => new EnemyAgentView
    {
        Handle = new object(), IsIdleArcher = true, Morale = morale, DistanceToPlayer = distance, HealthFraction = 1f,
        NearbyFoes = -1,
    };

    private static EnemyAgentView Soldier(bool hero = false, bool fleeing = false) => new EnemyAgentView
    {
        Handle = new object(), IsHero = hero, IsFleeing = fleeing, HealthFraction = 1f, Morale = 80f,
        DistanceToPlayer = 60f, NearbyFoes = -1,
    };

    private static FellFoesScan Scan(IEnumerable<EnemyAgentView> views, bool playerAlive = true, bool field = true,
        int reserves = 0, int others = 0) => new FellFoesScan
    {
        Enemies = views.ToList(), PlayerAlive = playerAlive, IsFieldBattle = field, EnemyReserves = reserves,
        OthersStanding = others,
    };

    // ------------------------------------------------------------ cornered routers

    private List<object> Cornered(IEnumerable<EnemyAgentView> views, out List<object> reflee, int others = 0)
    {
        var stop = new List<object>();
        reflee = new List<object>();
        FellFoesRules.PlanCornered(Scan(views, others: others), _s, _t, stop, reflee);
        return stop;
    }

    /// <summary>Two passes: the first records the router's health, the second sees it dropped to <paramref name="after"/>.</summary>
    private List<object> HitAndPlan(EnemyAgentView router, float after, params EnemyAgentView[] rest)
    {
        var all = new[] { router }.Concat(rest).ToList();
        Cornered(all, out _);
        router.HealthFraction = after;
        return Cornered(all, out _);
    }

    [DataTestMethod]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(0, false)]
    [DataRow(4, false)]
    [DataRow(-1, false)]
    public void Cornered_AHitWithFoesBeside_StopsOnlyInsideTheWindow(int foes, bool stops)
    {
        var r = Router(foes);

        var stop = HitAndPlan(r, 0.8f, Soldier());

        Assert.AreEqual(stops, stop.Contains(r.Handle));
        Assert.AreEqual(stops, _s.Cornered.Contains(r.Handle));
    }

    [TestMethod]
    public void Cornered_TheFirstSighting_IsNeverAHit()
    {
        var stop = Cornered(new[] { Router(2, 0.8f), Soldier() }, out _);

        Assert.AreEqual(0, stop.Count);
    }

    [TestMethod]
    public void Cornered_NoHealthDrop_IsNotAHit_AndARise_IsNot()
    {
        var r = Router(2);
        var rest = Soldier();
        Cornered(new[] { r, rest }, out _);
        Assert.AreEqual(0, Cornered(new[] { r, rest }, out _).Count, "same health");
        r.HealthFraction = 0.5f;
        Cornered(new[] { r, rest }, out _);
        r.HealthFraction = 0.9f;
        Assert.AreEqual(0, Cornered(new[] { r, rest }, out _).Count);
    }

    [TestMethod]
    public void Cornered_AnOrderRetreat_IsNotPanicked_AndIsLeftAlone()
    {
        var r = Router(2);
        r.IsPanicked = false;

        Assert.AreEqual(0, HitAndPlan(r, 0.8f, Soldier()).Count);
        Assert.AreEqual(0, _s.LastHealth.Count, "only panicked soldiers are tracked");
    }

    [TestMethod]
    public void Cornered_NaNHealth_FailsTheGate()
    {
        var r = Router(2);

        Assert.AreEqual(0, HitAndPlan(r, float.NaN, Soldier()).Count);
    }

    [TestMethod]
    public void Cornered_AWoundedRouter_BelowTheHealthFloor_IsNotStopped()
    {
        var r = Router(2);

        Assert.AreEqual(0, HitAndPlan(r, 0.1f, Soldier()).Count);
    }

    [TestMethod]
    public void Cornered_WhileOneEnemyStands_ACorneredRouterIsStopped()
    {
        var r = Router(2);

        Assert.AreEqual(1, HitAndPlan(r, 0.8f, Soldier()).Count);
    }

    [TestMethod]
    public void Cornered_ACreatureStandingCounts_AsSomebodyStanding()
    {
        var r = Router(2);
        Cornered(new[] { r }, out _, others: 1);
        r.HealthFraction = 0.8f;

        Assert.AreEqual(1, Cornered(new[] { r }, out _, others: 1).Count);
    }

    [TestMethod]
    public void Cornered_OnceEveryEnemyIsRunning_TheRalliedRunAgain_AndNoneIsStopped()
    {
        var hit = Router(2);
        var fresh = Router(2, 1f, 40f, 0f);
        var standing = Soldier();
        var all = new List<EnemyAgentView> { hit, fresh, standing };
        Cornered(all, out _);
        hit.HealthFraction = 0.8f;
        Cornered(all, out _);
        Assert.IsTrue(_s.Cornered.Contains(hit.Handle), "setup: the hit router was stopped while a soldier stood");
        hit.IsPanicked = false;
        hit.IsFleeing = false;
        standing.IsFleeing = true;
        fresh.HealthFraction = 0.7f;

        var stop = Cornered(all, out var reflee);

        Assert.AreEqual(0, stop.Count, "nobody new is stopped once the whole side has broken");
        CollectionAssert.AreEqual(new[] { hit.Handle }, reflee);
        Assert.AreEqual(0, _s.Cornered.Count);
    }

    [TestMethod]
    public void Cornered_ANewlyCorneredRouter_RalliesTheNearbyPanicked_NearestFirst_UpToTheCap()
    {
        var hit = Router(2, 1f, 0f, 0f);
        var near = Enumerable.Range(1, 6).Select(i => Router(0, 1f, i * 0.5f, 0f)).ToList();
        var far = Router(0, 1f, 40f, 0f);
        var all = new List<EnemyAgentView> { hit };
        all.AddRange(Enumerable.Reverse(near));
        all.Add(far);
        all.Add(Soldier());
        Cornered(all, out _);
        hit.HealthFraction = 0.8f;

        var stop = Cornered(all, out _);

        Assert.AreEqual(1 + _t.CorneredRallyCap, stop.Count);
        Assert.AreSame(hit.Handle, stop[0]);
        CollectionAssert.AreEqual(near.Take(_t.CorneredRallyCap).Select(v => v.Handle).ToList(), stop.Skip(1).ToList());
        Assert.IsFalse(_s.Cornered.Contains(far.Handle));
        Assert.AreEqual(1 + _t.CorneredRallyCap, _s.Cornered.Count, "rallied men join the cornered set");
    }

    [TestMethod]
    public void Cornered_ARallyCapOfZero_StopsOnlyTheHitRouter()
    {
        var tunables = FellFoesTunables.Create(corneredRallyCap: 0);
        var hit = Router(2);
        var mate = Router(0, 1f, 1f, 0f);
        var all = new List<EnemyAgentView> { hit, mate, Soldier() };
        FellFoesRules.PlanCornered(Scan(all), _s, tunables, new List<object>(), new List<object>());
        hit.HealthFraction = 0.8f;
        var stop = new List<object>();

        FellFoesRules.PlanCornered(Scan(all), _s, tunables, stop, new List<object>());

        CollectionAssert.AreEqual(new[] { hit.Handle }, stop);
    }

    [TestMethod]
    public void Cornered_ARallyOnlyTakesPanickedMenWithHealth_AndNotThoseOutOfRange()
    {
        var hit = Router(2);
        var calm = Router(0, 1f, 1f, 0f);
        calm.IsPanicked = false;
        var weak = Router(0, 0.1f, 1f, 0f);
        var nan = Router(0, 1f, float.NaN, float.NaN);
        var all = new List<EnemyAgentView> { hit, calm, weak, nan, Soldier() };
        Cornered(all, out _);
        hit.HealthFraction = 0.8f;

        var stop = Cornered(all, out _);

        CollectionAssert.AreEqual(new[] { hit.Handle }, stop);
    }

    [TestMethod]
    public void Cornered_AStoppedAgentWithNoFoesLeft_FleesAgain()
    {
        var r = Router(2);
        var rest = Soldier();
        HitAndPlan(r, 0.8f, rest);
        r.IsPanicked = false;
        r.IsFleeing = false;
        r.HoldFoes = 0;

        Cornered(new[] { r, rest }, out var reflee);

        CollectionAssert.AreEqual(new[] { r.Handle }, reflee);
        Assert.IsFalse(_s.Cornered.Contains(r.Handle));
    }

    [TestMethod]
    public void Cornered_AStoppedAgentStillInContact_KeepsFighting()
    {
        var r = Router(2);
        var rest = Soldier();
        HitAndPlan(r, 0.8f, rest);
        r.IsPanicked = false;
        r.IsFleeing = false;
        r.HoldFoes = 1;

        Cornered(new[] { r, rest }, out var reflee);

        Assert.AreEqual(0, reflee.Count);
        Assert.IsTrue(_s.Cornered.Contains(r.Handle));
    }

    [TestMethod]
    public void Cornered_ARalliedManEightMetresFromTheNearestFoe_IsKeptOnTheNextPass()
    {
        var hit = Router(2, 1f, 0f, 0f);
        var mate = Router(0, 1f, 3f, 0f);
        var rest = Soldier();
        var all = new List<EnemyAgentView> { hit, mate, rest };
        Cornered(all, out _);
        hit.HealthFraction = 0.8f;
        var stop = Cornered(all, out _);
        Assert.IsTrue(stop.Contains(mate.Handle), "setup: the mate rallied with the hit router");
        mate.IsPanicked = false;
        mate.IsFleeing = false;
        mate.NearbyFoes = 0;
        mate.HoldFoes = 1;

        Cornered(all, out var reflee);

        Assert.IsFalse(reflee.Contains(mate.Handle), "no foe inside the admission radius, one inside the hold radius: kept");
        Assert.IsTrue(_s.Cornered.Contains(mate.Handle));
    }

    [TestMethod]
    public void Cornered_AUnmeasuredHoldCount_KeepsTheTurnedMan()
    {
        var r = Router(2);
        var rest = Soldier();
        HitAndPlan(r, 0.8f, rest);
        r.IsPanicked = false;
        r.IsFleeing = false;
        r.HoldFoes = -1;

        Cornered(new[] { r, rest }, out var reflee);

        Assert.AreEqual(0, reflee.Count);
    }

    [TestMethod]
    public void Cornered_AnAgentGoneFromTheScan_DropsOutOfTheSetAndTheHealthMap()
    {
        var r = Router(2);
        var rest = Soldier();
        HitAndPlan(r, 0.8f, rest);

        Cornered(new[] { rest }, out var reflee);

        Assert.AreEqual(0, _s.Cornered.Count);
        Assert.AreEqual(0, _s.LastHealth.Count);
        Assert.AreEqual(0, reflee.Count, "a handle that left the scan is not unsteadied");
    }

    [TestMethod]
    public void Cornered_AStoppedAgentThatPanicsAgainOnItsOwn_IsForgottenThenJudgedAfresh()
    {
        var r = Router(2);
        var rest = Soldier();
        HitAndPlan(r, 0.8f, rest);
        r.IsPanicked = true;
        r.IsFleeing = true;
        r.NearbyFoes = 9;

        var stop = Cornered(new[] { r, rest }, out _);

        Assert.AreEqual(0, stop.Count);
        Assert.IsFalse(_s.Cornered.Contains(r.Handle));
    }

    // ------------------------------------------------------------ archers

    private List<object> PlanArchers(FellFoesScan scan, float roll, out List<object> release)
    {
        release = new List<object>();
        var hold = new List<object>();
        FellFoesRules.PlanArchers(scan, _s, _t, () => roll, release, hold);
        return hold;
    }

    [TestMethod]
    public void Archers_AnIdleArcherInRange_WinningTheRoll_IsHeld()
    {
        var a = Archer();

        var hold = PlanArchers(Scan(new[] { a }), 0f, out _);

        CollectionAssert.AreEqual(new[] { a.Handle }, hold);
        Assert.IsTrue(_s.Holds.ContainsKey(a.Handle));
    }

    [TestMethod]
    public void Archers_LosingTheRoll_HoldsNobody()
    {
        Assert.AreEqual(0, PlanArchers(Scan(new[] { Archer() }), 0.9f, out _).Count);
    }

    [TestMethod]
    public void Archers_ARollOfExactlyTheChance_LosesAndNaNLoses()
    {
        Assert.AreEqual(0, PlanArchers(Scan(new[] { Archer() }), _t.ArcherChance, out _).Count);
        Assert.AreEqual(0, PlanArchers(Scan(new[] { Archer() }), float.NaN, out _).Count);
    }

    [TestMethod]
    public void Archers_ABusyArcher_IsNotHeld()
    {
        var a = Archer();
        a.IsIdleArcher = false;

        Assert.AreEqual(0, PlanArchers(Scan(new[] { a }), 0f, out _).Count);
    }

    [DataTestMethod]
    [DataRow(50f)]
    [DataRow(10f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Archers_MoraleNotAboveTheFloor_OrNotANumber_IsNotHeld(float morale)
    {
        Assert.AreEqual(0, PlanArchers(Scan(new[] { Archer(20f, morale) }), 0f, out _).Count);
    }

    [DataTestMethod]
    [DataRow(35.5f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-1f)]
    public void Archers_DistanceOutOfRange_OrNotANumber_IsNotHeld(float distance)
    {
        Assert.AreEqual(0, PlanArchers(Scan(new[] { Archer(distance) }), 0f, out _).Count);
    }

    [TestMethod]
    public void Archers_TheRangeIsTheSiegeOneOutsideAFieldBattle_ChosenByTheRules()
    {
        var a = Archer(25f);

        Assert.AreEqual(0, PlanArchers(Scan(new[] { a }, field: false), 0f, out _).Count, "siege: 20 m");
        Assert.AreEqual(1, PlanArchers(Scan(new[] { a }), 0f, out _).Count, "field: 35 m");
    }

    [DataTestMethod]
    [DataRow("panicked")]
    [DataRow("fleeing")]
    [DataRow("horse")]
    public void Archers_PanickedFleeingOrStandingAgainstHorse_AreNotHeld(string why)
    {
        var a = Archer();
        a.IsPanicked = why == "panicked";
        a.IsFleeing = why == "fleeing";
        a.TeamStandsAgainstHorse = why == "horse";

        Assert.AreEqual(0, PlanArchers(Scan(new[] { a }), 0f, out _).Count);
    }

    [TestMethod]
    public void Archers_AlreadyHeld_IsNotHeldTwice()
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);

        Assert.AreEqual(0, PlanArchers(Scan(new[] { a }), 0f, out _).Count);
    }

    [TestMethod]
    public void Archers_WithNoPlayerAlive_HoldNobody_AndReleaseEveryHold()
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);

        var hold = PlanArchers(Scan(new[] { a }, playerAlive: false), 0f, out var release);

        Assert.AreEqual(0, hold.Count);
        CollectionAssert.AreEqual(new[] { a.Handle }, release);
        Assert.AreEqual(0, _s.Holds.Count);
    }

    [TestMethod]
    public void Archers_NoMoreThanTheCapHoldAtOnce_AndNoMoreThanThreeNewPerPass()
    {
        var archers = Enumerable.Range(0, 12).Select(i => Archer(10f + i)).ToList();
        var counts = new List<int>();

        for (var pass = 0; pass < 5; pass++)
            counts.Add(PlanArchers(Scan(archers), 0f, out _).Count);

        CollectionAssert.AreEqual(new[] { 3, 3, 2, 0, 0 }, counts);
        Assert.AreEqual(8, _s.Holds.Count);
    }

    [TestMethod]
    public void Archers_TheNearestAreHeldFirst()
    {
        var far = Archer(30f);
        var near = Archer(5f);
        var tunables = FellFoesTunables.Create(archerNewPerPass: 1);
        var hold = new List<object>();

        FellFoesRules.PlanArchers(Scan(new[] { far, near }), _s, tunables, () => 0f, new List<object>(), hold);

        CollectionAssert.AreEqual(new[] { near.Handle }, hold);
    }

    [TestMethod]
    public void Archers_AHoldIsReleased_WhenItsTimeRunsOut()
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);
        _s.Advance(_t.ArcherHoldSeconds + 0.5f);

        PlanArchers(Scan(new[] { a }), 0.99f, out var release);

        CollectionAssert.AreEqual(new[] { a.Handle }, release);
        Assert.AreEqual(0, _s.Holds.Count);
    }

    [TestMethod]
    public void Archers_AHoldIsKept_WhileItsTimeRuns()
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);
        _s.Advance(_t.ArcherHoldSeconds - 1f);

        PlanArchers(Scan(new[] { a }), 0.99f, out var release);

        Assert.AreEqual(0, release.Count);
        Assert.AreEqual(1, _s.Holds.Count);
    }

    [TestMethod]
    public void Archers_AHeldArcherGoneFromTheScan_IsReleasedAndForgotten()
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);

        PlanArchers(Scan(new EnemyAgentView[0]), 0.99f, out var release);

        CollectionAssert.AreEqual(new[] { a.Handle }, release);
        Assert.AreEqual(0, _s.Holds.Count);
    }

    [DataTestMethod]
    [DataRow("panicked")]
    [DataRow("fleeing")]
    [DataRow("horse")]
    public void Archers_AHeldArcherThatPanicsFleesOrWhoseTeamStands_IsReleased(string why)
    {
        var a = Archer();
        PlanArchers(Scan(new[] { a }), 0f, out _);
        a.IsPanicked = why == "panicked";
        a.IsFleeing = why == "fleeing";
        a.TeamStandsAgainstHorse = why == "horse";

        PlanArchers(Scan(new[] { a }), 0.99f, out var release);

        CollectionAssert.AreEqual(new[] { a.Handle }, release);
    }

    [TestMethod]
    public void State_TakeHolds_ReturnsEveryHeldHandleOnceAndClears()
    {
        var a = Archer();
        var b = Archer();
        PlanArchers(Scan(new[] { a, b }), 0f, out _);

        var taken = _s.TakeHolds();

        Assert.AreEqual(2, taken.Count);
        Assert.AreEqual(0, _s.TakeHolds().Count);
    }

    // ------------------------------------------------------------ stragglers

    private object? PlanStraggler(FellFoesScan scan) => FellFoesRules.PlanStraggler(scan, _s, _t);

    private static List<EnemyAgentView> Army(int n) => Enumerable.Range(0, n).Select(_ => Soldier()).ToList();

    private object? SendAfterTheHold(FellFoesScan scan)
    {
        Assert.IsNull(PlanStraggler(scan), "the first low-count pass only starts the clock");
        _s.Advance(_t.StragglerHoldSeconds + 0.5f);
        return PlanStraggler(scan);
    }

    [TestMethod]
    public void Stragglers_AfterTheHold_OneIsSent()
    {
        PlanStraggler(Scan(Army(100)));
        var few = Army(3);

        var sent = SendAfterTheHold(Scan(few));

        Assert.IsNotNull(sent);
        Assert.IsTrue(few.Any(v => ReferenceEquals(v.Handle, sent)));
    }

    [TestMethod]
    public void Stragglers_BeforeTheHoldElapses_NobodyIsSent()
    {
        PlanStraggler(Scan(Army(100)));
        var few = Scan(Army(3));
        PlanStraggler(few);
        _s.Advance(_t.StragglerHoldSeconds - 1f);

        Assert.IsNull(PlanStraggler(few));
    }

    [TestMethod]
    public void Stragglers_OneSendPerTick_NeverTheSameAgentTwice()
    {
        PlanStraggler(Scan(Army(100)));
        var scan = Scan(Army(3));
        var first = SendAfterTheHold(scan);
        var second = PlanStraggler(scan);
        var third = PlanStraggler(scan);
        var fourth = PlanStraggler(scan);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.IsNotNull(third);
        Assert.IsNull(fourth);
        Assert.AreEqual(3, new[] { first, second, third }.Distinct().Count());
    }

    [TestMethod]
    public void Stragglers_AnArmyThatNeverReachedThePeak_IsNotCleanedUp()
    {
        PlanStraggler(Scan(Army(_t.StragglerPeakMin - 1)));

        Assert.IsNull(SendAfterTheHold(Scan(Army(3))));
    }

    [TestMethod]
    public void Stragglers_TheCountRisingAboveTheLimit_RestartsTheClock()
    {
        PlanStraggler(Scan(Army(100)));
        PlanStraggler(Scan(Army(3)));
        _s.Advance(_t.StragglerHoldSeconds + 1f);
        Assert.IsNull(PlanStraggler(Scan(Army(4))));

        Assert.IsNull(PlanStraggler(Scan(Army(3))), "the clock restarted when the count rose");
    }

    [TestMethod]
    public void Stragglers_ReservesStillToSpawn_OrUnknown_BlockTheCleanup()
    {
        PlanStraggler(Scan(Army(100)));

        Assert.IsNull(SendAfterTheHold(Scan(Army(3), reserves: 12)));
        Assert.IsNull(SendAfterTheHold(Scan(Army(3), reserves: -1)));
    }

    [TestMethod]
    public void Stragglers_AHeroOnTheField_BlocksTheCleanup()
    {
        PlanStraggler(Scan(Army(100)));
        var few = new List<EnemyAgentView> { Soldier(), Soldier(), Soldier(hero: true) };

        Assert.IsNull(SendAfterTheHold(Scan(few)));
    }

    [TestMethod]
    public void Stragglers_OutsideAFieldBattle_NothingIsSent()
    {
        PlanStraggler(Scan(Army(100)));

        Assert.IsNull(SendAfterTheHold(Scan(Army(3), field: false)));
    }

    [TestMethod]
    public void Stragglers_NobodyLeft_NothingIsSent()
    {
        PlanStraggler(Scan(Army(100)));

        Assert.IsNull(SendAfterTheHold(Scan(Army(0))));
    }

    [TestMethod]
    public void Stragglers_AgentsAlreadyFleeing_AreCountedButNeverChosen()
    {
        PlanStraggler(Scan(Army(100)));
        var few = new List<EnemyAgentView> { Soldier(fleeing: true), Soldier(), Soldier(fleeing: true) };

        var sent = SendAfterTheHold(Scan(few));

        Assert.AreSame(few[1].Handle, sent);
    }

    [TestMethod]
    public void Stragglers_AllAlreadyFleeing_NothingToSend()
    {
        PlanStraggler(Scan(Army(100)));
        var few = new List<EnemyAgentView> { Soldier(fleeing: true), Soldier(fleeing: true) };

        Assert.IsNull(SendAfterTheHold(Scan(few)));
    }

    [TestMethod]
    public void Stragglers_ACreatureStillStanding_BlocksTheCleanup()
    {
        PlanStraggler(Scan(Army(100)));

        Assert.IsNull(SendAfterTheHold(Scan(Army(3), others: 1)));
    }

    [TestMethod]
    public void Stragglers_ARalliedManSentOff_LeavesTheCorneredSet()
    {
        PlanStraggler(Scan(Army(100)));
        var few = Army(1);
        _s.Cornered.Add(few[0].Handle);

        var sent = SendAfterTheHold(Scan(few));

        Assert.AreSame(few[0].Handle, sent);
        Assert.IsFalse(_s.Cornered.Contains(few[0].Handle));
    }

    [TestMethod]
    public void Stragglers_ANonFiniteClockStep_DoesNotAdvanceTheClock()
    {
        _s.Advance(5f);
        _s.Advance(float.NaN);
        _s.Advance(float.PositiveInfinity);
        _s.Advance(-3f);

        Assert.AreEqual(5f, _s.Clock);
    }

    [TestMethod]
    public void Peak_TracksTheLargestCountSeen()
    {
        PlanStraggler(Scan(Army(10)));
        PlanStraggler(Scan(Army(70)));
        PlanStraggler(Scan(Army(20)));

        Assert.AreEqual(70, _s.PeakEnemies);
    }
}
