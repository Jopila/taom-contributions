namespace TAOM.Adapters;

/// <summary>
/// One freshly built agent as the gait variety feature reads and changes it (ADR-007). The wrapper is
/// short-lived: the mission behavior builds one per <c>OnAgentBuild</c> call and drops it.
/// </summary>
public interface IGaitVarietyAgent
{
    bool IsHuman { get; }

    bool IsMount { get; }

    /// <summary>The main agent, or an agent a player controls.</summary>
    bool IsPlayerControlled { get; }

    /// <summary>True when the agent has a mount, or its spawn equipment carries a horse.</summary>
    bool IsMounted { get; }

    /// <summary>The name of the agent's current action set, for example <c>as_orc_warrior</c>.</summary>
    string ActionSetName { get; }

    /// <summary>The troop's <c>StringId</c>.</summary>
    string CharacterId { get; }

    /// <summary><c>Agent.BodyPropertiesSeed</c>: stable per soldier, unlike <c>Agent.Index</c>.</summary>
    int BodyPropertiesSeed { get; }

    /// <summary>The step size vanilla gave the agent at spawn: <c>Character.GetStepSize()</c>.</summary>
    float VanillaStepSize { get; }

    /// <summary>
    /// Replaces the agent's action set and step size with ONE <c>Agent.SetActionSet</c> call. Natively
    /// that clears both action channels and restarts the walk phase at 0.
    /// </summary>
    void ApplyGait(string actionSetId, float stepSize);
}
