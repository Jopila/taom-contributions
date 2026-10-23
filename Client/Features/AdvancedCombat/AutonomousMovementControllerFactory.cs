using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.AdvancedCombat;

/// <summary>The explicit rage-mode controller view SubModule adds to every mission (ADR-013); core's null factory adds none.</summary>
internal sealed class AutonomousMovementControllerFactory : IAutonomousMovementControllerFactory
{
    public MissionBehavior? Create() => new AutonomousMovementPlayerController();
}
