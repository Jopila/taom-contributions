using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Where a standing wall plants itself against horse: chokes first, then forest, then the slope
/// point, then where it stands. The decisions are pure (floats and <c>Vec2</c>): the engine scan
/// that feeds them is <c>StandGroundScan</c>. The enemy is to the north (+y) throughout.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StandGroundTests
{
    private static readonly EngagementTunables Engagement = EngagementTunables.Default;           // cap 60, hold 50
    private static readonly RaceTunables Race = new RaceTunables(formUpSeconds: 5f, formUpSecondsPerUnit: 0f, marginSeconds: 0f);
    private static readonly Vec2 Ours = new Vec2(0f, 0f);
    private static readonly Vec2 EnemyCentre = new Vec2(0f, 300f);
    private static readonly Vec2 Sideways = new Vec2(1f, 0f);
    private static readonly List<float> Scratch = new List<float>();

    // A 100-man wall walking at 3 m/s with a 100 m closest foot (not inside the 50 m hold).
    private static StandSituation Situation(float speed = 3f, float maxWidth = 40f, Vec2? hill = null, float closestFoot = 100f) =>
        new StandSituation(Ours, speed, ourUnits: 100, maxWidth, EnemyCentre, hill ?? Ours, closestFoot);

    private static GroundCandidate PickCandidate(GroundCandidate[] candidates, StandSituation s, params EnemyMover[] enemies) =>
        StandGround.Pick(candidates, in s, in Engagement, in Race, enemies, Scratch);

    private static GroundKind Pick(GroundCandidate[] candidates, StandSituation s, out Vec2 position, params EnemyMover[] enemies)
    {
        var picked = PickCandidate(candidates, s, enemies);
        position = picked.Position;
        return picked.Kind;
    }

    // A choke whose direction points at the enemy, as vanilla's eligibility test reads it (dot >= 0.5).
    private static GroundCandidate Choke(Vec2 at, float width = 20f, bool insurmountable = false) =>
        GroundCandidate.Choke(at, new Vec2(0f, 1f), width, insurmountable);

    [TestMethod]
    public void Pick_Choke_ReturnsTheChokesOwnFacingWidthAndKind()
    {
        var direction = new Vec2(0f, 1f);
        var candidates = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), direction, 17f, insurmountable: true) };

        var picked = PickCandidate(candidates, Situation());

        Assert.AreEqual(GroundKind.Choke, picked.Kind);
        Assert.AreEqual(direction, picked.Direction);
        Assert.AreEqual(17f, picked.Width);
        Assert.IsTrue(picked.Insurmountable);
    }

    [TestMethod]
    public void Pick_HillAndHere_CarryNoFacingAndNoWidth()
    {
        var hill = PickCandidate(new GroundCandidate[0], Situation(hill: new Vec2(5f, 25f)));
        var here = PickCandidate(new GroundCandidate[0], Situation());

        Assert.AreEqual(GroundKind.Hill, hill.Kind);
        Assert.AreEqual(GroundKind.Here, here.Kind);
        Assert.AreEqual(0f, hill.Width);
        Assert.AreEqual(0f, here.Width);
        Assert.AreEqual(Vec2.Zero, hill.Direction);
        Assert.AreEqual(Vec2.Zero, here.Direction);
    }

    [TestMethod]
    public void HoldOf_ChokeThatCanBeCrossed_HoldsItsPointFacingItsDirectionAtItsWidth()
    {
        var c = GroundCandidate.Choke(new Vec2(4f, 30f), new Vec2(0f, 1f), 20f, insurmountable: false);

        var hold = StandGround.HoldOf(in c, EnemyCentre);

        Assert.AreEqual(new Vec2(4f, 30f), hold.Position);
        Assert.AreEqual(new Vec2(0f, 1f), hold.Facing);
        Assert.AreEqual(20f, hold.Width);
    }

    [TestMethod]
    public void HoldOf_InsurmountableChoke_StandsHalfAWidthTowardTheEnemyFacingIt()
    {
        // Vanilla BehaviorDefend.cs:36-39: the position moves Width * 0.5 along (enemy - position).Normalized().
        var c = GroundCandidate.Choke(new Vec2(0f, 30f), new Vec2(0f, -1f), 20f, insurmountable: true);

        var hold = StandGround.HoldOf(in c, EnemyCentre);

        Assert.AreEqual(0f, hold.Position.x, 1e-4f);
        Assert.AreEqual(40f, hold.Position.y, 1e-4f);
        Assert.AreEqual(0f, hold.Facing.x, 1e-4f);
        Assert.AreEqual(1f, hold.Facing.y, 1e-4f);
        Assert.AreEqual(20f, hold.Width);
    }

    [TestMethod]
    public void HoldOf_InsurmountableChokeUnderTheEnemyCentre_KeepsItsPointAndOwnDirection()
    {
        var c = GroundCandidate.Choke(new Vec2(0f, 300f), new Vec2(0f, 1f), 20f, insurmountable: true);

        var hold = StandGround.HoldOf(in c, EnemyCentre);

        Assert.AreEqual(new Vec2(0f, 300f), hold.Position);
        Assert.AreEqual(new Vec2(0f, 1f), hold.Facing);
    }

    [TestMethod]
    public void HoldOf_ForestHillAndHere_KeepTheirPointWithNoFacingAndNoWidth()
    {
        var forest = GroundCandidate.Forest(new Vec2(-10f, 40f));
        var hill = new GroundCandidate(GroundKind.Hill, new Vec2(5f, 25f), Vec2.Zero, 0f, false);

        foreach (var c in new[] { forest, hill })
        {
            var hold = StandGround.HoldOf(in c, EnemyCentre);

            Assert.AreEqual(c.Position, hold.Position);
            Assert.AreEqual(Vec2.Zero, hold.Facing);
            Assert.AreEqual(0f, hold.Width);
        }
    }

    [TestMethod]
    public void Pick_InsurmountableChokeWhoseHoldPointIsBeyondTheCap_IsNotPicked()
    {
        // Centre 50 m out (inside the 60 m cap), but the hold point is half a 40 m width toward the enemy: 70 m.
        var candidates = new[] { GroundCandidate.Choke(new Vec2(0f, 50f), new Vec2(0f, -1f), 40f, insurmountable: true) };

        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(), out _));
    }

    [TestMethod]
    public void Pick_InsurmountableChokeWhoseHoldPointIsInsideTheCap_IsPickedAtItsCentre()
    {
        var centre = new Vec2(0f, 20f);
        var candidates = new[] { GroundCandidate.Choke(centre, new Vec2(0f, -1f), 20f, insurmountable: true) };

        var kind = Pick(candidates, Situation(), out var position);

        Assert.AreEqual(GroundKind.Choke, kind);
        Assert.AreEqual(centre, position);
    }

    [TestMethod]
    public void CanTake_GroundInsideTheCapWithTheRaceWon_IsTrue()
    {
        var horse = new[] { new EnemyMover(new Vec2(0f, 600f), speed: 15f) };

        Assert.IsTrue(HighGroundRace.CanTake(30f, 100f, 3f, 100, in Engagement, in Race, new Vec2(0f, 30f), horse, Scratch));
    }

    [TestMethod]
    public void CanTake_EnemyFootInsideTheHoldDistanceOrTheRaceLost_IsFalse()
    {
        var horse = new[] { new EnemyMover(new Vec2(0f, 180f), speed: 15f) };

        Assert.IsFalse(HighGroundRace.CanTake(30f, 50f, 3f, 100, in Engagement, in Race, new Vec2(0f, 30f), new EnemyMover[0], Scratch));
        Assert.IsFalse(HighGroundRace.CanTake(30f, 100f, 3f, 100, in Engagement, in Race, new Vec2(0f, 30f), horse, Scratch));
    }

    [TestMethod]
    public void Pick_NoCandidatesNoEnemies_StandsHere()
    {
        var kind = Pick(new GroundCandidate[0], Situation(), out var position);

        Assert.AreEqual(GroundKind.Here, kind);
        Assert.AreEqual(Ours, position);
    }

    [TestMethod]
    public void Pick_ChokeForestAndHillAllReachable_PrefersTheChoke()
    {
        var choke = new Vec2(10f, 30f);
        var candidates = new[] { GroundCandidate.Forest(new Vec2(-10f, 20f)), Choke(choke) };

        var kind = Pick(candidates, Situation(hill: new Vec2(5f, 10f)), out var position);

        Assert.AreEqual(GroundKind.Choke, kind);
        Assert.AreEqual(choke, position);
    }

    [TestMethod]
    public void Pick_NoChoke_PrefersTheForestOverTheHill()
    {
        var forest = new Vec2(-10f, 40f);

        var kind = Pick(new[] { GroundCandidate.Forest(forest) }, Situation(hill: new Vec2(5f, 10f)), out var position);

        Assert.AreEqual(GroundKind.Forest, kind);
        Assert.AreEqual(forest, position);
    }

    [TestMethod]
    public void Pick_OnlyTheSlopePoint_TakesTheHill()
    {
        var hill = new Vec2(5f, 25f);

        var kind = Pick(new GroundCandidate[0], Situation(hill: hill), out var position);

        Assert.AreEqual(GroundKind.Hill, kind);
        Assert.AreEqual(hill, position);
    }

    [TestMethod]
    public void Pick_SlopePointIsOurOwnGround_IsHereNotHill()
    {
        // HighGroundOf answers the formation's own position when the enemy is out of view.
        var kind = Pick(new GroundCandidate[0], Situation(hill: Ours), out _);

        Assert.AreEqual(GroundKind.Here, kind);
    }

    [TestMethod]
    public void Pick_SeveralChokes_TakesTheNearestEligibleOne()
    {
        var near = new Vec2(0f, 20f);
        var far = new Vec2(0f, 50f);

        var kind = Pick(new[] { Choke(far), Choke(near) }, Situation(), out var position);

        Assert.AreEqual(GroundKind.Choke, kind);
        Assert.AreEqual(near, position);
    }

    [TestMethod]
    public void Pick_ChokeFacingSideways_IsNotEligible()
    {
        var candidates = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), Sideways, 20f, insurmountable: false) };

        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(), out _));
    }

    [TestMethod]
    public void Pick_ChokeFacingAwayFromTheEnemy_IsNotEligibleUnlessInsurmountable()
    {
        var facingAway = new Vec2(0f, -1f);
        var open = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), facingAway, 20f, insurmountable: false) };
        var walled = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), facingAway, 20f, insurmountable: true) };

        Assert.AreEqual(GroundKind.Here, Pick(open, Situation(), out _));
        Assert.AreEqual(GroundKind.Choke, Pick(walled, Situation(), out _));
    }

    [TestMethod]
    public void Pick_ChokeFacingAtTheDotThreshold_IsEligible()
    {
        // Exactly 60 degrees off the line to the enemy: the dot is 0.5, and vanilla accepts >= 0.5.
        var direction = new Vec2(-0.8660254f, 0.5f);
        var candidates = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), direction, 20f, insurmountable: false) };

        Assert.AreEqual(GroundKind.Choke, Pick(candidates, Situation(), out _));
    }

    [TestMethod]
    public void Pick_ChokeWiderThanTheInfantryCanFill_IsNotEligible()
    {
        var candidates = new[] { Choke(new Vec2(0f, 30f), width: 41f) };

        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(maxWidth: 40f), out _));
    }

    [TestMethod]
    public void Pick_ChokeAsWideAsTheInfantryMaximum_IsEligible()
    {
        var candidates = new[] { Choke(new Vec2(0f, 30f), width: 40f) };

        Assert.AreEqual(GroundKind.Choke, Pick(candidates, Situation(maxWidth: 40f), out _));
    }

    [TestMethod]
    public void Pick_NaNWidthOrFacing_IsNotEligible()
    {
        var nanWidth = new[] { Choke(new Vec2(0f, 30f), width: float.NaN) };
        var nanFacing = new[] { GroundCandidate.Choke(new Vec2(0f, 30f), new Vec2(float.NaN, float.NaN), 20f, false) };

        Assert.AreEqual(GroundKind.Here, Pick(nanWidth, Situation(), out _));
        Assert.AreEqual(GroundKind.Here, Pick(nanFacing, Situation(), out _));
    }

    [TestMethod]
    public void Pick_EnemyOnTopOfTheChoke_FallsBackToTheNextKind()
    {
        // The choke's own point coincides with the enemy centre: no direction to face, never eligible.
        var onEnemy = new Vec2(0f, 30f);
        var forest = new Vec2(10f, 30f);
        var candidates = new[] { Choke(onEnemy), GroundCandidate.Forest(forest) };
        var s = new StandSituation(Ours, 3f, ourUnits: 100, maxWidth: 40f, enemyCentre: onEnemy, hill: Ours, closestEnemyFootMetres: 100f);

        Assert.AreEqual(GroundKind.Forest, Pick(candidates, s, out var position));
        Assert.AreEqual(forest, position);
    }

    [TestMethod]
    public void Pick_GroundBeyondTheCap_IsNotChosen()
    {
        var candidates = new[] { Choke(new Vec2(0f, 61f)), GroundCandidate.Forest(new Vec2(0f, 70f)) };

        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(hill: new Vec2(0f, 80f)), out _));
    }

    [TestMethod]
    public void Pick_GroundExactlyAtTheCap_IsChosen()
    {
        var candidates = new[] { Choke(new Vec2(0f, 60f)) };

        Assert.AreEqual(GroundKind.Choke, Pick(candidates, Situation(), out _));
    }

    [TestMethod]
    public void Pick_EnemyFootAlreadyInsideTheHoldDistance_StandsHere()
    {
        var candidates = new[] { Choke(new Vec2(0f, 30f)), GroundCandidate.Forest(new Vec2(10f, 20f)) };

        var kind = Pick(candidates, Situation(hill: new Vec2(0f, 20f), closestFoot: 50f), out var position);

        Assert.AreEqual(GroundKind.Here, kind);
        Assert.AreEqual(Ours, position);
    }

    [TestMethod]
    public void Pick_EnemyFootJustBeyondTheHoldDistance_StillMarches()
    {
        var candidates = new[] { Choke(new Vec2(0f, 30f)) };

        Assert.AreEqual(GroundKind.Choke, Pick(candidates, Situation(closestFoot: 50.5f), out _));
    }

    [TestMethod]
    public void Pick_CavalryReachesTheChokeFirst_LosesTheRaceAndFallsToTheNextKind()
    {
        // 30 m at 3 m/s = 10 s plus 5 s form-up. Horse 150 m away at 15 m/s arrive in 10 s.
        var choke = new Vec2(0f, 30f);
        var forest = new Vec2(0f, -15f);
        var horse = new EnemyMover(new Vec2(0f, 180f), speed: 15f);

        var kind = Pick(new[] { Choke(choke), GroundCandidate.Forest(forest) }, Situation(), out var position, horse);

        Assert.AreEqual(GroundKind.Forest, kind);
        Assert.AreEqual(forest, position);
    }

    [TestMethod]
    public void Pick_ARaceAFootOnlyRaceWouldWin_IsLostToTheHorse()
    {
        // The same march against the same enemy, once as foot (2 m/s from 150 m: 75 s, we are
        // ready in 15 s) and once as horse (15 m/s: 10 s, we are not).
        var candidates = new[] { Choke(new Vec2(0f, 30f)) };
        var asFoot = new EnemyMover(new Vec2(0f, 180f), speed: 2f);
        var asHorse = new EnemyMover(new Vec2(0f, 180f), speed: 15f);

        Assert.AreEqual(GroundKind.Choke, Pick(candidates, Situation(), out _, asFoot));
        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(), out _, asHorse));
    }

    [TestMethod]
    public void Pick_HorseFarEnoughAway_LetsTheWallMarch()
    {
        var horse = new EnemyMover(new Vec2(0f, 600f), speed: 15f);

        Assert.AreEqual(GroundKind.Choke, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(), out _, horse));
    }

    [TestMethod]
    public void Pick_RaceLostToTheNearChoke_StillTakesAFartherOneTheHorseCannotBeatUsTo()
    {
        // Horse come from the east at 10 m/s: the near choke (30 m east, 120 m from them) is theirs in 12 s,
        // the far one (30 m west, 180 m from them) in 18 s, and we are ready after 10 + 5 s.
        var near = new Vec2(30f, 0f);
        var far = new Vec2(-30f, 0f);
        var horse = new EnemyMover(new Vec2(150f, 0f), speed: 10f);

        var kind = Pick(new[] { Choke(near), Choke(far) }, Situation(), out var position, horse);

        Assert.AreEqual(GroundKind.Choke, kind);
        Assert.AreEqual(far, position);
    }

    [TestMethod]
    public void Pick_NaNOurSpeed_StandsHere()
    {
        var candidates = new[] { Choke(new Vec2(0f, 30f)), GroundCandidate.Forest(new Vec2(0f, 20f)) };

        Assert.AreEqual(GroundKind.Here, Pick(candidates, Situation(speed: float.NaN), out _));
    }

    [TestMethod]
    public void Pick_ZeroOurSpeed_StandsHere()
    {
        Assert.AreEqual(GroundKind.Here, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(speed: 0f), out _));
    }

    [TestMethod]
    public void Pick_NaNEnemySpeed_IsNotARacer()
    {
        var horse = new EnemyMover(new Vec2(0f, 100f), speed: float.NaN);

        Assert.AreEqual(GroundKind.Choke, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(), out _, horse));
    }

    [TestMethod]
    public void Pick_NaNEnemyPosition_IsNotARacer()
    {
        var horse = new EnemyMover(new Vec2(float.NaN, float.NaN), speed: 15f);

        Assert.AreEqual(GroundKind.Choke, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(), out _, horse));
    }

    [TestMethod]
    public void Pick_NaNClosestFoot_StandsHere()
    {
        Assert.AreEqual(GroundKind.Here, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(closestFoot: float.NaN), out _));
    }

    [TestMethod]
    public void Pick_NoFootKnown_IsDecidedByTheCapAlone()
    {
        Assert.AreEqual(GroundKind.Choke, Pick(new[] { Choke(new Vec2(0f, 30f)) }, Situation(closestFoot: float.PositiveInfinity), out _));
    }

    [TestMethod]
    public void Pick_ReusesTheScratchList_NoStateLeaksBetweenCalls()
    {
        var horse = new EnemyMover(new Vec2(0f, 180f), speed: 15f);
        var candidates = new[] { Choke(new Vec2(0f, 30f)) };

        Pick(candidates, Situation(), out _, horse);
        var second = Pick(candidates, Situation(), out _);

        Assert.AreEqual(GroundKind.Choke, second);
    }
}
