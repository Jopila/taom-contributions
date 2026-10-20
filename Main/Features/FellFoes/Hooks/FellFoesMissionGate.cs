using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Features.FellFoes.Hooks;

/// <summary>
/// Which missions Fell Foes may touch: field battles, sieges and sally-outs in Battle mode with real combat,
/// never an arena or tournament, a town, a hideout or a conversation, and never in a co-op session or where a
/// co-op host owns the battle (the soldiers' morale and orders are the host's to decide), and never once the mission has
/// ended (the result is decided, and a write then would turn captures into routs).
/// The battle allowlist is <see cref="MountDespawnMissionGate"/>'s, shared with the corpse cleanup: an
/// allowlist, so a mission type a later engine version adds is excluded until someone adds it on purpose.
/// </summary>
public static class FellFoesMissionGate
{
    public static bool IsEligible(Mission mission, ICoopSessionProvider coop) =>
        !IsStandingDown(coop) && MountDespawnMissionGate.IsEligible(mission) && !mission.MissionEnded;

    /// <summary>The same stand-down test the stuck battle sweep uses (a live session, or a co-op mod TAOM cannot probe).</summary>
    public static bool IsStandingDown(ICoopSessionProvider coop) => coop.IsSessionActive || coop.ShouldDeferToHost;
}
