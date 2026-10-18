using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Short-lived <see cref="Agent"/> view for the <c>[AnimVariety]</c> probe (<see cref="IAnimVarietyAgent"/>).</summary>
public sealed class AnimVarietyAgent : IAnimVarietyAgent
{
    private readonly Agent _agent;

    public AnimVarietyAgent(Agent agent) => _agent = agent;

    public object Handle => _agent;

    // The engine's IsActive() answers for a recycled slot's new tenant (#592), so the identity check is required.
    public bool IsActive => _agent != null && _agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(_agent);

    public int ActionSetKey => _agent.ActionSet.GetHashCode();

    public int Channel1ActionIndex => _agent.GetCurrentAction(1).Index;

    public string Channel1ActionName => _agent.GetCurrentAction(1).GetName();

    public bool Channel1IsAttackRelease => _agent.GetCurrentActionStage(1) == Agent.ActionStage.AttackRelease;

    public float Channel1Progress => _agent.GetCurrentActionProgress(1);
}
