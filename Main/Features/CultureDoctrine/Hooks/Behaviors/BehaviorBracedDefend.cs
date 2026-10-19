using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// A wall that holds a position and braces against horse: <c>BehaviorDefend</c>
/// (`BehaviorDefend.cs`) with the anti-cavalry square from <see cref="BraceDecision"/>. Marches
/// loosely to <see cref="DefensePosition"/> (or holds where it stands), faces the closest enemy,
/// closes into ShieldWall on arrival (Line without shields, Loose under fire at a distance) and
/// goes Square while a horse formation rides at it inside <c>CavalryMattersMetres</c> or did in
/// the last few seconds (<see cref="EnemyScan"/>), holding its ground in the square. It faces
/// the nearest enemy FOOT formation (<see cref="TargetSelection"/>), or the horse while braced.
/// A wall that holds a choke (<see cref="DefenseWidth"/> above zero, the horse stand) is held the
/// way vanilla's <c>BehaviorDefend</c> holds one: it faces <see cref="DefenseFacing"/> (or the live
/// enemy for an insurmountable one), fills the gap's width with <c>FormOrderCustom</c> every tick
/// at the position, and never goes Square (a square would open
/// the gap): it keeps its ShieldWall or Line. Arrangement is re-issued only
/// on a change of stance: <c>Formation.SetArrangementOrder</c> expires the query cache and
/// recomputes the arrangement on every real change (`Formation.cs:744-772`). Weight 1, as
/// <c>BehaviorDefend</c>.
/// </summary>
public sealed class BehaviorBracedDefend : TaomBehaviorBase
{
    // BehaviorDefend's own arrival radius, 10 m.
    private const float ArrivedDistanceSquared = 100f;
    private const float ArmsLengthSquared = 100f;

    private float _lastChargeSignal = -1f;
    private WallStance _stance = WallStance.Loose;
    private bool _braced;
    private WorldPosition _bracePoint = WorldPosition.Invalid;
    private Formation? _target;

    /// <summary>Set by the tactic before the plan is applied; invalid means "where you stand".</summary>
    public WorldPosition DefensePosition = WorldPosition.Invalid;

    /// <summary>Set by the tactic with <see cref="DefensePosition"/>: the direction to face on a held
    /// choke; zero (the default) faces the enemy as every other wall does.</summary>
    public Vec2 DefenseFacing;

    /// <summary>Set by the tactic with <see cref="DefensePosition"/>: the frontage of a held choke;
    /// zero (the default) is no choke, so the wall keeps its width and squares up against horse.</summary>
    public float DefenseWidth;

    /// <summary>Set by the tactic for an insurmountable choke: face the live enemy as vanilla does
    /// (<c>FacingOrderLookAtEnemy</c>) rather than <see cref="DefenseFacing"/>'s fixed direction.</summary>
    public bool DefenseLookAtEnemy;

    public BehaviorBracedDefend(Formation formation)
        : base(formation)
    {
        CalculateCurrentOrder();
    }

    protected override float Weigh() => 1f;

    protected override void Plan()
    {
        var formation = Formation;
        var direction = DefenseFacing.LengthSquared > 1e-4f
            ? DefenseFacing
            : Facing(formation, _braced ? formation.CachedClosestEnemyFormation?.Formation : _target != null && _target.CountOfUnits > 0 ? _target : null);
        var target = DefensePosition.IsValid ? DefensePosition : WallStances.Here(formation);
        // A braced wall does not walk in a square: it stands where the charge found it, on the
        // point taken when the brace began (the drifting average would creep the square).
        if (_braced && _bracePoint.IsValid)
            target = _bracePoint;
        CurrentOrder = MovementOrder.MovementOrderMove(target);
        CurrentFacingOrder = DefenseLookAtEnemy ? FacingOrder.FacingOrderLookAtEnemy : FacingOrder.FacingOrderLookAtDirection(direction);
    }

    protected override void Activate()
    {
        _lastChargeSignal = -1f;
        _braced = false;
        _bracePoint = WorldPosition.Invalid;
        _target = null;
        _stance = WallStance.Loose;
        Formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLoose);
        Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        Formation.SetFormOrder(FormOrder.FormOrderWide);
        SetStatus(_stance.ToString());
    }

    protected override void OnActiveTick()
    {
        var formation = Formation;
        var q = formation.QuerySystem;
        _target = EnemyScan.Pick(formation, Targets, in Engagement, _braced, out var cavalryThreat);
        var braced = WallStances.Braced(cavalryThreat, ref _lastChargeSignal);
        var holdingChoke = DefenseWidth > 0f;
        if (braced && !_braced)
        {
            // Brace where the wall stands unless it is already at its position, which it keeps; a
            // held choke is always kept.
            var atPosition = WallStances.DistanceSquaredTo(formation, CurrentOrder.GetPosition(formation)) < ArrivedDistanceSquared;
            _bracePoint = atPosition || holdingChoke ? WorldPosition.Invalid : WallStances.Here(formation);
        }
        else if (!braced)
        {
            _bracePoint = WorldPosition.Invalid;
        }
        _braced = braced;
        var enemy = _target;
        var enemyDistanceSquared = enemy == null ? float.MaxValue : WallStances.DistanceSquaredTo(formation, enemy.CachedMedianPosition.AsVec2);
        var situation = new WallSituation(
            atPosition: WallStances.DistanceSquaredTo(formation, CurrentOrder.GetPosition(formation)) < ArrivedDistanceSquared,
            hasShield: q.HasShield,
            cavalryChargeInbound: _braced,
            // BehaviorDefend's own bar, a tenth lower once loose so the line does not flap.
            underRangedAttack: enemy != null && q.UnderRangedAttackRatio > (_stance == WallStance.Loose ? 0.1f : 0.2f),
            enemyAtArmsLength: enemyDistanceSquared <= ArmsLengthSquared,
            withinShieldDistance: false);
        var stance = BraceDecision.Defend(in situation, _braced, holdingChoke);
        if (stance != _stance)
        {
            _stance = stance;
            formation.SetArrangementOrder(WallStances.Arrangement(stance));
            SetStatus(stance.ToString());
        }
        // BehaviorDefend.cs:55, 65-78 re-issues the custom width every tick while at the position: a
        // later SetArrangementOrder makes the engine rewrite a Custom width from the file count
        // (Formation.cs:758-761, 2926-2928), so a one-off order would let the frontage drift.
        if (holdingChoke && situation.AtPosition)
            formation.SetFormOrder(FormOrder.FormOrderCustom(DefenseWidth));
    }

    public override void ResetBehavior()
    {
        base.ResetBehavior();
        DefensePosition = WorldPosition.Invalid;
        DefenseFacing = Vec2.Zero;
        DefenseWidth = 0f;
        DefenseLookAtEnemy = false;
    }

    // BehaviorDefend's facing rule: toward the enemy unless the wall already faces it within
    // 60 degrees, in which case its own direction, so the line does not twitch.
    private static Vec2 Facing(Formation formation, Formation? enemy)
    {
        if (enemy == null)
            return formation.Direction;
        var toEnemy = enemy.CachedMedianPosition.AsVec2 - formation.CachedAveragePosition;
        if (!(toEnemy.LengthSquared > 1e-4f))
            return formation.Direction;
        var normalized = toEnemy.Normalized();
        return formation.Direction.DotProduct(normalized) < 0.5f ? normalized : formation.Direction;
    }
}
