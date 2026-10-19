using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>What a standing wall plants itself on; the declaration order is the status line's.</summary>
public enum GroundKind
{
    Here,
    Choke,
    Forest,
    Hill,
}

/// <summary>One piece of ground a wall could stand on, copied once from the scene's tactical
/// positions and regions. <see cref="Direction"/> and <see cref="Width"/> matter for a choke
/// only (the entity's facing and its mouth); a forest is just a point. Immutable.</summary>
public readonly struct GroundCandidate
{
    public GroundCandidate(GroundKind kind, Vec2 position, Vec2 direction, float width, bool insurmountable)
    {
        Kind = kind;
        Position = position;
        Direction = direction;
        Width = width;
        Insurmountable = insurmountable;
    }

    public GroundKind Kind { get; }
    public Vec2 Position { get; }
    public Vec2 Direction { get; }
    public float Width { get; }
    public bool Insurmountable { get; }

    public static GroundCandidate Choke(Vec2 position, Vec2 direction, float width, bool insurmountable) =>
        new GroundCandidate(GroundKind.Choke, position, direction, width, insurmountable);

    public static GroundCandidate Forest(Vec2 position) =>
        new GroundCandidate(GroundKind.Forest, position, Vec2.Zero, 0f, false);
}

/// <summary>An enemy formation that could reach the ground before we are formed up: where it is
/// and its top speed. Cavalry is one of these; the stand's whole point is that it races.</summary>
public readonly struct EnemyMover
{
    public EnemyMover(Vec2 position, float speed)
    {
        Position = position;
        Speed = speed;
    }

    public Vec2 Position { get; }
    public float Speed { get; }
}

/// <summary>What the infantry knows when it picks a place to stand.</summary>
public readonly struct StandSituation
{
    public StandSituation(
        Vec2 ours, float ourSpeed, int ourUnits, float maxWidth, Vec2 enemyCentre, Vec2 hill, float closestEnemyFootMetres)
    {
        Ours = ours;
        OurSpeed = ourSpeed;
        OurUnits = ourUnits;
        MaxWidth = maxWidth;
        EnemyCentre = enemyCentre;
        Hill = hill;
        ClosestEnemyFootMetres = closestEnemyFootMetres;
    }

    public Vec2 Ours { get; }
    public float OurSpeed { get; }
    public int OurUnits { get; }
    public float MaxWidth { get; }
    public Vec2 EnemyCentre { get; }
    public Vec2 Hill { get; }
    public float ClosestEnemyFootMetres { get; }
}

/// <summary>How the wall holds the ground it picked, as vanilla's <c>BehaviorDefend</c> would
/// (<see cref="StandGround.HoldOf"/>): where to stand, which way to face (zero keeps the wall's
/// own facing toward the enemy) and the frontage to fill (zero keeps the wall's own).</summary>
public readonly struct StandHold
{
    public StandHold(Vec2 position, Vec2 facing, float width)
    {
        Position = position;
        Facing = facing;
        Width = width;
    }

    public Vec2 Position { get; }
    public Vec2 Facing { get; }
    public float Width { get; }
}

/// <summary>
/// Where the infantry stands against horse, in the order a captain would look: a choke point
/// the horse cannot ride around, then forest that breaks a charge, then the slope point the
/// other position-holding tactics use, then where the wall already is. A candidate counts only
/// if it is inside the march cap, no enemy foot is already on us
/// (<see cref="HighGroundRace.WorthGoing"/>) and the wall can get there and form up before any
/// enemy formation, horse included (<see cref="HighGroundRace.Decide"/>); the nearest such
/// candidate of the best kind wins. Time to reach is the straight line over top speed, which
/// is optimistic for the horse, so a march that wins on paper is still a march the horse
/// could contest on a winding path. Pure: no engine type but <c>Vec2</c>, every comparison a
/// positive requirement (a NaN width, facing, speed or position never qualifies), and the only
/// writes go to the caller's scratch list.
/// </summary>
public static class StandGround
{
    // Vanilla's facing test: `TacticHoldChokePoint.cs:146-153`.
    private const float MinFacingDot = 0.5f;

    // HighGroundOf answers the formation's own position when the enemy is out of view; that is
    // "here", not a hill.
    private const float MinHillMetres = 1f;

    private const float MinDirectionLengthSquared = 1e-4f;

    /// <summary>The ground to stand on: the chosen candidate itself (so a choke keeps its facing,
    /// width and whether it can be crossed), or a Hill or Here candidate at the slope point or the
    /// wall's own position with no facing and no width.</summary>
    public static GroundCandidate Pick(
        GroundCandidate[] candidates, in StandSituation s, in EngagementTunables engagement, in RaceTunables race,
        IReadOnlyList<EnemyMover> enemies, List<float> scratch)
    {
        if (TryNearest(candidates, GroundKind.Choke, in s, in engagement, in race, enemies, scratch, out var picked))
            return picked;
        if (TryNearest(candidates, GroundKind.Forest, in s, in engagement, in race, enemies, scratch, out picked))
            return picked;
        var hillDistance = s.Ours.Distance(s.Hill);
        if (hillDistance >= MinHillMetres && HighGroundRace.CanTake(hillDistance, s.ClosestEnemyFootMetres, s.OurSpeed, s.OurUnits, in engagement, in race, s.Hill, enemies, scratch))
            return new GroundCandidate(GroundKind.Hill, s.Hill, Vec2.Zero, 0f, false);
        return new GroundCandidate(GroundKind.Here, s.Ours, Vec2.Zero, 0f, false);
    }

    /// <summary>How to hold a picked candidate, vanilla's way (`BehaviorDefend.cs:20-34, 65-78`). A
    /// choke that can be crossed is held at its point, facing its own direction, filling its width.
    /// An insurmountable one is held half a width toward the enemy, facing the enemy's centre, filling
    /// its width. Anything else keeps its point with a zero facing and width, which the wall reads as
    /// "face the enemy as you do, keep your own frontage". Deliberate departure from vanilla: the
    /// hold point is fixed here at decision time, where vanilla recomputes it every tick from the
    /// live enemy centre; a wall that kept sliding toward the enemy would leave the gap. (The facing
    /// of an insurmountable choke does track the live enemy, see <c>BehaviorBracedDefend</c>.)</summary>
    public static StandHold HoldOf(in GroundCandidate c, Vec2 enemyCentre)
    {
        if (c.Kind != GroundKind.Choke)
            return new StandHold(c.Position, Vec2.Zero, 0f);
        if (!c.Insurmountable)
            return new StandHold(c.Position, c.Direction, c.Width);
        var toEnemy = enemyCentre - c.Position;
        if (!(toEnemy.LengthSquared > MinDirectionLengthSquared))
            return new StandHold(c.Position, c.Direction, c.Width);
        var facing = toEnemy.Normalized();
        return new StandHold(c.Position + facing * (c.Width * 0.5f), facing, c.Width);
    }

    private static bool TryNearest(
        GroundCandidate[] candidates, GroundKind kind, in StandSituation s, in EngagementTunables engagement, in RaceTunables race,
        IReadOnlyList<EnemyMover> enemies, List<float> scratch, out GroundCandidate picked)
    {
        picked = default;
        var bestDistance = float.PositiveInfinity;
        var found = false;
        for (var i = 0; i < candidates.Length; i++)
        {
            var c = candidates[i];
            if (c.Kind != kind || (kind == GroundKind.Choke && !ChokeEligible(in c, in s)))
                continue;
            // Qualified at the point the wall will actually hold (HoldOf), which the anchor's
            // once-a-second re-check races too, so the two never disagree.
            var hold = HoldOf(in c, s.EnemyCentre).Position;
            var distance = s.Ours.Distance(hold);
            if (!(distance < bestDistance) || !HighGroundRace.CanTake(distance, s.ClosestEnemyFootMetres, s.OurSpeed, s.OurUnits, in engagement, in race, hold, enemies, scratch))
                continue;
            bestDistance = distance;
            picked = c;
            found = true;
        }
        return found;
    }

    /// <summary>Keeps vanilla's width and facing tests (`TacticHoldChokePoint.cs:145-154`) made
    /// pure: the mouth faces the enemy (dot at least 0.5, in either direction for an insurmountable
    /// one) and is no wider than the infantry can fill. Vanilla's distance gate (`139-144`) is
    /// replaced by the march cap and the race in <see cref="HighGroundRace.CanTake"/>.</summary>
    private static bool ChokeEligible(in GroundCandidate c, in StandSituation s)
    {
        if (!(c.Width <= s.MaxWidth))
            return false;
        var toEnemy = s.EnemyCentre - c.Position;
        if (!(toEnemy.LengthSquared > MinDirectionLengthSquared))
            return false;
        var dot = toEnemy.Normalized().DotProduct(c.Direction);
        if (c.Insurmountable)
            dot = dot < 0f ? -dot : dot;
        return dot >= MinFacingDot;
    }
}
