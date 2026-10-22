namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// How a TAOM mission behavior must act when its mission runs inside a co-op session (#967, C7).
/// No mission runs on the co-op authority process: every peer runs its own battle and spawns only the
/// troops it owns, while the elected battle host also spawns the AI and the enemies. Every other agent on
/// a peer is a puppet (<c>AgentControllerType.None</c>) driven by the network.
/// </summary>
public enum CoopMissionRole
{
    /// <summary>Touches only this peer's own view, log or files. Runs everywhere unchanged.</summary>
    Local,

    /// <summary>
    /// Acts on agents, and must act only on agents this peer owns. Added in a session; each agent is
    /// checked live through <see cref="ICoopMissionAuthority"/> at the point it would act.
    /// </summary>
    AgentOwner,

    /// <summary>
    /// Wants the battle host. Added in a session. The only member today (CultureDoctrine) runs on every
    /// peer on purpose, because tactics move only AI-controlled agents and on a guest those are puppets.
    /// </summary>
    BattleHost,

    /// <summary>
    /// Mutates shared world or peer state in a way a second peer would duplicate or contradict. Not added
    /// at all in a co-op session.
    /// </summary>
    Unsafe,
}
