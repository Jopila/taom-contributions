using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Short-lived <see cref="Agent"/> view for the gait variety feature (<see cref="IGaitVarietyAgent"/>).</summary>
public sealed class GaitVarietyAgent : IGaitVarietyAgent
{
    private readonly Agent _agent;

    public GaitVarietyAgent(Agent agent) => _agent = agent;

    public bool IsHuman => _agent.IsHuman;

    public bool IsMount => _agent.IsMount;

    // Agent.Build sets Controller (Agent.cs:5206) and the Controller setter assigns Mission.MainAgent for a
    // Player controller (Agent.cs:1254-1255), both before Mission.SpawnAgent raises OnAgentBuild
    // (Mission.cs:4390), so IsMainAgent already holds for the player's agent. IsPlayerControlled adds the
    // agents a player drives without being the main agent (Agent.cs:626-636: Controller Player, or a mission
    // peer's agent), so both are asked (v1.5.5).
    public bool IsPlayerControlled => _agent.IsMainAgent || _agent.IsPlayerControlled;

    // The mount link lives in native code, so a rider is detected twice: the cached mount agent and the
    // horse slot of the spawn equipment (Mission.SpawnAgent fills both before OnAgentBuild).
    public bool IsMounted => _agent.MountAgent != null
        || (_agent.SpawnEquipment != null && !_agent.SpawnEquipment[EquipmentIndex.Horse].IsEmpty);

    public string ActionSetName => _agent.ActionSet.GetName();

    public string CharacterId => _agent.Character?.StringId;

    public int BodyPropertiesSeed => _agent.BodyPropertiesSeed;

    // The value Mission.SpawnAgent passed to CreateAgent (Mission.cs:4180, v1.5.5).
    public float VanillaStepSize => _agent.Character?.GetStepSize() ?? float.NaN;

    public void ApplyGait(string actionSetId, float stepSize)
    {
        var set = MBActionSet.GetActionSet(actionSetId);
        if (!set.IsValid || set.GetName() != actionSetId)
            throw new InvalidOperationException("No action set named " + actionSetId);

        // Same call vanilla makes for a suffixed set right after spawn (Mission.cs:4531-4534, v1.5.5).
        var data = _agent.Monster.FillAnimationSystemData(set, stepSize, hasClippingPlane: false);
        _agent.SetActionSet(ref data);
    }
}
