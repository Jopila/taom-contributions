using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>The battle action bar's mission view SubModule adds to every mission (ADR-013: the view is client-only).
/// Returns MissionBehavior, never a View type, so the caller's IL names no client assembly. Null: add nothing.</summary>
public interface IBattleActionBarViewFactory
{
    MissionBehavior? Create();
}
