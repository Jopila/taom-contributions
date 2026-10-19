using TAOM.Core.Validation;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// The polearm half of the horse stand: every AI human in a formation holding a Dismount order,
/// which for a <c>dismount: true</c> team is its whole standing wall (the infantry and the riders
/// who got off), reaches for the spear in preference to the sword on his hip. A post-pass on the agent-stat models, after
/// the base model has reset <c>AiWeaponFavorMultiplierPolearm</c> to 1 on every update
/// (`AgentStatCalculateModel.cs:226-228`), so it cannot compound. Runs wherever
/// <c>Agent.UpdateAgentProperties</c> runs, including the dismount itself
/// (<c>Agent.OnDismount</c>), which can be off the main thread, so it reads one team reference,
/// one formation reference and a 4-byte enum, and the favour comes from an immutable catalog.
/// </summary>
public static class StandWeaponFavor
{
    /// <summary>The favour for an AI human (<see cref="Apply"/> has already returned for anything else)
    /// in a formation ordered to dismount: <paramref name="favor"/>; 1 (no effect) otherwise and for a
    /// favour that is not a finite number above 1.</summary>
    public static float Multiplier(bool onPlayerTeam, bool dismountOrdered, float favor)
    {
        if (onPlayerTeam || !dismountOrdered)
            return 1f;
        return FiniteFloatValidator.IsFinite(favor) && favor > 1f ? favor : 1f;
    }

    public static void Apply(Agent agent, AgentDrivenProperties properties, float favor)
    {
        if (!(favor > 1f) || agent == null || !agent.IsHuman)
            return;
        var team = agent.Team;
        var formation = agent.Formation;
        if (team == null || formation == null)
            return;
        var multiplier = Multiplier(team.IsPlayerTeam, formation.RidingOrder == RidingOrder.RidingOrderDismount, favor);
        if (multiplier > 1f)
            properties.AiWeaponFavorMultiplierPolearm = multiplier;
    }
}
