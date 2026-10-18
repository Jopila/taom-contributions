namespace TAOM.Adapters;

/// <summary>
/// One agent as the <c>[AnimVariety]</c> probe reads it (ADR-007). The wrapper is cheap and short-lived:
/// the mission behavior builds one per human agent per tick, so the service keys its state by
/// <see cref="Handle"/> (the engine agent object, never <c>Agent.Index</c>).
/// </summary>
public interface IAnimVarietyAgent
{
    /// <summary>The underlying engine agent object, compared by reference.</summary>
    object Handle { get; }

    /// <summary>Alive AND still the occupant of its slot (<c>AgentSlotIdentity</c>).</summary>
    bool IsActive { get; }

    /// <summary>The engine index of the agent's action set.</summary>
    int ActionSetKey { get; }

    int Channel1ActionIndex { get; }

    /// <summary>The name of the channel-1 action; marshals a string, so read it only on a change.</summary>
    string Channel1ActionName { get; }

    bool Channel1IsAttackRelease { get; }

    float Channel1Progress { get; }
}
