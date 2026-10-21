namespace TAOM.Features.CoopInterop;

/// <summary>Who drives a party or clan: a human player, the AI, or nobody TAOM can tell.</summary>
public enum PlayerControl
{
    /// <summary>The AI drives it. Without a co-op session, everything but this peer's own party and clan.</summary>
    NotPlayer,

    /// <summary>A human player drives it: this peer's own, or a remote co-op player's.</summary>
    Player,

    /// <summary>A co-op session is live but its player registry is unreadable.</summary>
    Unknown,
}
