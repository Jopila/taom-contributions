namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// How a TAOM mission behavior must act when its mission runs inside a co-op session (#967, C7).
/// No mission runs on the co-op authority process: every peer runs its own battle and spawns only the
/// troops it owns, while the elected battle host also spawns the AI and the enemies. Every other agent on
/// a peer is a puppet (<c>AgentControllerType.None</c>) driven by the network. The caveat: an owned mount
/// reads None for up to 2 s during co-op's synthetic mount turn, so a mount's controller alone does not
/// prove it is a puppet.
/// </summary>
public enum CoopMissionRole
{
    /// <summary>
    /// Touches only this peer's own view, log or files, or acts only through an invariant that already
    /// keeps puppets out (CultureDoctrine: tactics move only AI-controlled agents). Runs everywhere unchanged.
    /// </summary>
    Local,

    /// <summary>
    /// Acts only on agents this peer owns: through a live CoopMissionOwnership check, or by an invariant
    /// its doc row names (an AI-controller gate, the player team). Added in a session.
    /// </summary>
    AgentOwner,

    /// <summary>
    /// Not added in a session: it changes shared state, or it never runs in one anyway.
    /// </summary>
    Unsafe,
}
