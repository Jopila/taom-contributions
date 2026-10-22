using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// "Does this peer own this agent?" for TAOM mission behaviors (#967, C7).
/// In a co-op battle every peer runs its own mission: it spawns and drives only the troops it owns, and
/// every other agent on it is a puppet the network moves. A TAOM behavior that attacks, heals, buffs or
/// morale-hits an agent must act only where this answers true, or the owner and a guest both apply it.
///
/// Without a live co-op session the answer is true and no reflection runs, so singleplayer is unchanged.
/// It is read LIVE and never cached across frames: a host handoff moves authority mid-battle. Safe to
/// call from an engine callback that runs off the main thread (the registry takes its own lock).
/// </summary>
public interface ICoopMissionAuthority
{
    /// <summary>
    /// True when this peer owns <paramref name="agent"/> (or there is no session). A puppet answers false.
    /// An agent co-op has not registered yet follows co-op's own fallback: a human is owned when it has a
    /// controller, a mount follows its rider.
    /// </summary>
    bool IsLocallyControlled(Agent? agent);
}
