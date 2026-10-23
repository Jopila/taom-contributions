using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>The explicit controller instance SubModule adds to every mission besides the engine's [DefaultView]
/// one (ADR-013: the type is client-only). Returns MissionBehavior, never a View type, so the caller's IL names
/// no client assembly. Null: add nothing.</summary>
public interface IAutonomousMovementControllerFactory
{
    MissionBehavior? Create();
}
