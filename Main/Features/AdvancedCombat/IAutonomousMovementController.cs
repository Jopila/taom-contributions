using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// The rage-mode player controller as the Warg code drives it (ADR-013). The controller is a MissionView, so it
/// lives in TAOM.Client. Core finds it with Mission.GetMissionBehavior of this interface, which returns the first
/// behavior that implements it: the engine's [DefaultView] instance, the one the class lookup returned before
/// (docs/features/warg-combat.md). Null in a mission without it: a dedicated server, or TAOM.Client missing.
/// An engine-boundary interface: its callers are BT tasks and a MissionLogic that already hold the Agent.
/// </summary>
public interface IAutonomousMovementController : IMissionBehavior
{
    Agent? TargetAgent { get; set; }
    Vec3 CustomLookDir { get; set; }
    void Disable();
    /// <summary>True: vanilla's MissionMainAgentController off, then this on (rage). False: the reverse.</summary>
    void SwitchMainAgentController(bool switchToSpecialController);
}
