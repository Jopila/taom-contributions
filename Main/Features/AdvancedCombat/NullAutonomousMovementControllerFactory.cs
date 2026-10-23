using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>Core's default (ADR-013): no controller view. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullAutonomousMovementControllerFactory : IAutonomousMovementControllerFactory
{
    public MissionBehavior? Create() => null;
}
