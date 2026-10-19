using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// Where a position-holding tactic (the Dwarven wall, the Elven ring) stands, decided the way a
/// captain would: march to the navmesh high ground only if the foot can get there and form up
/// before the enemy's foot arrives (<see cref="HighGroundRace"/>, cavalry not a racer), and only
/// when no enemy foot is already on us (<see cref="HighGroundRace.WorthGoing"/>,
/// <see cref="EngagementTunables"/>), otherwise form where it stands. The ground itself is the
/// best slope toward the enemy INSIDE the cap (<see cref="HighGroundOf"/>): the engine's own
/// query searches a square scaled to half the distance to the enemy, which at deployment range
/// names a hill 150 m away and never the knoll 30 m off; horse do not end a march, the wall
/// squares up against them where it is. While marching the race is re-checked once a second and a lost race
/// turns into a hold on the spot; once arrived, or once the closest enemy is inside
/// <c>BehaviorHoldHighGround</c>'s lock radius (<c>max(0.8 * archers' missile range, 30 m)</c>,
/// `BehaviorHoldHighGround.cs:35-46`), the position is locked for good. One instance per tactic,
/// touched only from that tactic's tick (the team-AI tick); the ETA list is reused, so a tick
/// allocates nothing.
///
/// <para>
/// Stand mode (<see cref="Stand"/>, the horse stand of <c>HorseStand</c>) swaps the ground
/// and the race: the wall takes the best of the scene's chokes, its forests, the slope point and
/// its own ground (<see cref="StandGround"/>, candidates copied once by
/// <see cref="StandGroundScan"/>), and enemy melee cavalry races it like foot does. It is off
/// for every other user of this class, which keeps the foot-and-archer race above untouched.
/// </para>
/// </summary>
public sealed class HighGroundAnchor
{
    private enum State
    {
        Undecided,
        Marching,
        Holding,
        Arrived,
    }

    // BehaviorDefend's own "at position" radius (`BehaviorDefend.cs:50`, 100 m squared).
    private const float ArrivedDistance = 10f;
    private const float MinLockRadius = 30f;

    private static readonly string[] StatusNames = { "Undecided", "Marching", "Holding", "Arrived" };
    private static readonly string[] Suffixes = { ":Undecided", ":Marching", ":Holding", ":Arrived" };

    // ":Stand:<kind>:<state>", indexed kind * 4 + state; built once so the status line allocates nothing.
    private static readonly string[] StandSuffixes = BuildStandSuffixes();

    private static string[] BuildStandSuffixes()
    {
        var kinds = new[] { GroundKind.Here, GroundKind.Choke, GroundKind.Forest, GroundKind.Hill };
        var all = new string[kinds.Length * StatusNames.Length];
        for (var k = 0; k < kinds.Length; k++)
            for (var s = 0; s < StatusNames.Length; s++)
                all[(int)kinds[k] * StatusNames.Length + s] = ":Stand:" + kinds[k] + ":" + StatusNames[s];
        return all;
    }

    // One enemy team holds 10 formations (Team.cs:279); FillMovers walks every enemy team, so the lists
    // can pass 10 when the enemy side has several, and then simply grow.
    private readonly List<float> _enemyEtas = new List<float>(10);
    private readonly List<EnemyMover> _movers = new List<EnemyMover>(10);
    private GroundCandidate[]? _candidates;
    private bool _standing;
    private GroundKind _kind;
    private Vec2 _facing;
    private float _width;
    private bool _lookAtEnemy;
    private State _state;
    private Formation? _for;
    private WorldPosition _position = WorldPosition.Invalid;

    /// <summary>The state's name, a constant per state: the status line compares it by
    /// reference once a second to see whether the tactic's line needs rebuilding.</summary>
    public string Status => StatusNames[(int)_state];

    /// <summary>The same with the colon, for the tactic's status line; in stand mode
    /// <c>:Stand:Choke|Forest|Hill|Here:&lt;state&gt;</c>.</summary>
    public string Suffix => _standing ? StandSuffixes[(int)_kind * StatusNames.Length + (int)_state] : Suffixes[(int)_state];

    /// <summary>Which way the held ground must be faced, for a choke only: the wall's own facing
    /// toward the enemy is kept when this is zero.</summary>
    public Vec2 Facing => _facing;

    /// <summary>The frontage to fill, for a choke only; zero keeps the wall's own.</summary>
    public float Width => _width;

    /// <summary>True when the held ground is an insurmountable choke, which vanilla faces with
    /// <c>FacingOrderLookAtEnemy</c> (it tracks the live enemy) rather than a fixed direction.</summary>
    public bool LookAtEnemy => _lookAtEnemy;

    /// <summary>Switches stand mode on. The tactic latches its stand, so the gate's flicker never
    /// switches it back; only <see cref="EndStand"/>, at the tactic's cancel, does. The first call
    /// forgets the decision so the next <see cref="Resolve"/> picks the ground again under the
    /// stand's rules.</summary>
    public void Stand()
    {
        if (_standing)
            return;
        _standing = true;
        _state = State.Undecided;
        _kind = GroundKind.Here;
    }

    /// <summary>Leaves stand mode when the tactic is cancelled, so a later pick starts clean: the
    /// non-stand ground rules, state Undecided, kind Here, no facing and no width.</summary>
    public void EndStand()
    {
        _standing = false;
        _state = State.Undecided;
        _kind = GroundKind.Here;
        _facing = Vec2.Zero;
        _width = 0f;
        _lookAtEnemy = false;
    }

    /// <summary>The position to defend, decided or re-checked at a phase apply.
    /// <paramref name="anchor"/> is the formation whose high ground is meant (the archers for a
    /// ring, the infantry for a wall); <paramref name="infantry"/> is the formation that must
    /// get there.</summary>
    public WorldPosition Resolve(Formation infantry, Formation anchor, Formation? archers, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (_for != infantry)
        {
            _for = infantry;
            _state = State.Undecided;
        }
        switch (_state)
        {
            case State.Undecided:
                DecideGround(infantry, anchor, team, in race, in engagement);
                break;
            case State.Marching:
                // Still tracking the high ground while the enemy is far, as BehaviorHoldHighGround does.
                if (EnemyBeyondLockRadius(infantry, archers))
                    DecideGround(infantry, anchor, team, in race, in engagement);
                break;
        }
        return _position;
    }

    /// <summary>Once a second while marching: true when the race is now lost and the plan must
    /// re-apply with the hold position this returns through <see cref="Resolve"/>.</summary>
    public bool Tick(Formation infantry, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (_state != State.Marching || _for != infantry)
            return false;
        if (infantry.CachedAveragePosition.DistanceSquared(_position.AsVec2) < ArrivedDistance * ArrivedDistance)
        {
            _state = State.Arrived;
            return false;
        }
        if (CanTake(infantry, _position.AsVec2, team, in race, in engagement))
            return false;
        HoldHere(infantry);
        return true;
    }

    private void DecideGround(Formation infantry, Formation anchor, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        if (_standing)
            DecideStand(infantry, anchor, team, in race, in engagement);
        else
            Decide(infantry, HighGroundOf(anchor, in engagement), team, in race, in engagement);
    }

    // Stand mode: the best ground by StandGround's order, the race counting enemy melee cavalry.
    private void DecideStand(Formation infantry, Formation anchor, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        var candidates = _candidates ??= StandGroundScan.Copy(team);
        var hill = HighGroundOf(anchor, in engagement);
        FillMovers(team);
        var enemyCentre = team.QuerySystem.AverageEnemyPosition;
        var situation = new StandSituation(
            infantry.CachedAveragePosition, infantry.QuerySystem.MovementSpeedMaximum, infantry.CountOfUnits, infantry.MaximumWidth,
            enemyCentre, hill.AsVec2, EnemyScan.ClosestFootDistance(infantry));
        var picked = StandGround.Pick(candidates, in situation, in engagement, in race, _movers, _enemyEtas);
        if (picked.Kind == GroundKind.Here)
        {
            HoldHere(infantry);
            return;
        }
        var hold = StandGround.HoldOf(in picked, enemyCentre);
        _kind = picked.Kind;
        _facing = hold.Facing;
        _width = hold.Width;
        _lookAtEnemy = picked.Insurmountable;
        _state = State.Marching;
        _position = infantry.CachedMedianPosition;
        _position.SetVec2(hold.Position);
    }

    private void Decide(Formation infantry, WorldPosition ground, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        _facing = Vec2.Zero;
        _width = 0f;
        _lookAtEnemy = false;
        if (CanTake(infantry, ground.AsVec2, team, in race, in engagement))
        {
            _state = State.Marching;
            _position = ground;
        }
        else
        {
            HoldHere(infantry);
        }
    }

    private void HoldHere(Formation infantry)
    {
        _state = State.Holding;
        _kind = GroundKind.Here;
        _facing = Vec2.Zero;
        _width = 0f;
        _lookAtEnemy = false;
        _position = Here(infantry);
    }

    // The ground is worth the march and the race against every mover is won: one composition for
    // the stand's picker and this re-check.
    private bool CanTake(Formation infantry, Vec2 target, Team team, in RaceTunables race, in EngagementTunables engagement)
    {
        FillMovers(team);
        return HighGroundRace.CanTake(
            infantry.CachedAveragePosition.Distance(target), EnemyScan.ClosestFootDistance(infantry),
            infantry.QuerySystem.MovementSpeedMaximum, infantry.CountOfUnits, in engagement, in race, target, _movers, _enemyEtas);
    }

    // Every enemy foot and archer formation (in stand mode, melee cavalry too) with where it is and
    // how fast it goes; horse archers wheel at range and never race for ground.
    private void FillMovers(Team team)
    {
        _movers.Clear();
        var teams = team.Mission.Teams;
        for (var t = 0; t < teams.Count; t++)
        {
            var other = teams[t];
            if (other == team || !other.IsEnemyOf(team))
                continue;
            var formations = other.FormationsIncludingSpecialAndEmpty;
            for (var i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f.CountOfUnits <= 0)
                    continue;
                var q = f.QuerySystem;
                if (!q.IsInfantryFormation && !q.IsRangedFormation && !(_standing && q.IsCavalryFormation))
                    continue;
                _movers.Add(new EnemyMover(f.CachedMedianPosition.AsVec2, q.MovementSpeedMaximum));
            }
        }
    }

    private static bool EnemyBeyondLockRadius(Formation infantry, Formation? archers)
    {
        var enemy = infantry.CachedClosestEnemyFormation;
        if (enemy == null)
            return true;
        var radius = MathF.Max(archers != null ? archers.QuerySystem.MissileRangeAdjusted * 0.8f : 0f, MinLockRadius);
        return infantry.CachedAveragePosition.DistanceSquared(enemy.Formation.CachedMedianPosition.AsVec2) > radius * radius;
    }

    /// <summary>The best slope toward the enemy inside the cap, as a world position: the
    /// engine's own search (<c>Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare</c>,
    /// what <c>HighGroundCloseToForeseenBattleGround</c> calls, `FormationQuerySystem.cs:637-643`)
    /// on a square whose half-side is the cap over root two, so every candidate is inside it.
    /// With no enemy in view the formation's own ground. Native, called at a phase apply only.</summary>
    public static WorldPosition HighGroundOf(Formation formation, in EngagementTunables engagement)
    {
        var center = formation.CachedMedianPosition;
        center.SetVec2(formation.CachedAveragePosition);
        var reference = formation.QuerySystem.Team.MedianTargetFormationPosition;
        if (!reference.IsValid)
            return center;
        return formation.Team.Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare(ref center, engagement.HighGroundMaxMetres * 0.7071f, ref reference);
    }

    private static WorldPosition Here(Formation formation)
    {
        var position = formation.CachedMedianPosition;
        position.SetVec2(formation.CachedAveragePosition);
        return position;
    }
}
