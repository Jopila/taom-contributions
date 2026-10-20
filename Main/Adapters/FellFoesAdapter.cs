using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CultureDoctrine.Hooks;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of Fell Foes (ADR-007). The scan walks <c>Team.ActiveAgents</c> of the teams hostile to the
/// player's once, reading only what the requested rules need (the cornered-radius foe count only for panicked
/// soldiers, the hold-radius count only for turned ones, the archer reads only inside archer range). Every write re-checks that the handle is still the
/// live occupant of its engine slot (<see cref="AgentSlotIdentity"/>, #592) and runs on the main thread
/// from <c>OnMissionTick</c>; <see cref="MissionThreadGuard"/> logs a call from anywhere else.
/// Engine members (v1.5.5): <c>CommonAIComponent.StopRetreating/Retreat/IsPanicked/IsRetreating</c>,
/// <c>CommonAIComponent.Morale/RecoveryMorale</c>, <c>Agent.SetTargetAgent/SetAutomaticTargetSelection/ImmediateEnemy/IsRangedCached/
/// IsFadingOut/IsRunningAway</c>, <c>Mission.OnAgentFleeing</c>, <c>Mission.GetNearbyEnemyAgentCount</c>.
/// </summary>
public sealed class FellFoesAdapter : IFellFoesAdapter
{
    // Cached once: a method group would allocate a delegate on every write (sibling convention in CreatureSiegeRoleMissionBehavior).
    private readonly Action<string> _warn;

    public FellFoesAdapter(IModLogger logger) => _warn = (logger ?? throw new ArgumentNullException(nameof(logger))).LogWarning;

    public FellFoesScan Scan(FellFoesScanRequest request)
    {
        var scan = new FellFoesScan();
        var mission = Mission.Current;
        var playerTeam = mission?.PlayerTeam;
        if (mission == null || playerTeam == null) return scan;

        var main = mission.MainAgent;
        scan.PlayerAlive = main != null && AgentSlotIdentity.IsCurrentOccupant(main) && main.IsActive();
        scan.IsFieldBattle = mission.IsFieldBattle;
        scan.EnemyReserves = EnemyReserves(mission, playerTeam.Side);

        Vec2? playerPosition = scan.PlayerAlive ? main!.Position.AsVec2 : (Vec2?)null;
        var views = new List<EnemyAgentView>();
        foreach (var team in mission.Teams)
        {
            if (team == null || !team.IsEnemyOf(playerTeam)) continue;
            var stands = request.ArcherReadRange > 0f && TeamTacticProbe.IsHorseStanding(team);
            var agents = team.ActiveAgents;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || !agent.IsActive()) continue;
                if (!agent.IsHuman)
                {
                    // A creature on the enemy team stands on its own until it runs; the cleanup waits for it.
                    if (!agent.IsRunningAway) scan.OthersStanding++;
                    continue;
                }

                views.Add(View(mission, team, agent, request, main, playerPosition, stands));
            }
        }

        scan.Enemies = views;
        return scan;
    }

    private static EnemyAgentView View(
        Mission mission, Team team, Agent agent, FellFoesScanRequest request, Agent? main, Vec2? playerPosition, bool teamStands)
    {
        var ai = agent.CommonAIComponent;
        var position = agent.Position.AsVec2;
        var view = new EnemyAgentView
        {
            Handle = agent,
            IsHero = agent.IsHero,
            IsPanicked = ai != null && ai.IsPanicked && ai.IsRetreating,
            IsFleeing = agent.IsRunningAway,
            HealthFraction = agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : float.NaN,
            Morale = agent.GetMorale(),
            DistanceToPlayer = playerPosition.HasValue ? position.Distance(playerPosition.Value) : float.NaN,
            TeamStandsAgainstHorse = teamStands,
            X = position.x,
            Y = position.y,
        };

        // Admission (a panicked man, the cornered radius) and retention (a turned man, the wider hold radius) are two reads.
        if (request.CorneredRadius > 0f && view.IsPanicked)
            view.NearbyFoes = mission.GetNearbyEnemyAgentCount(team, position, request.CorneredRadius);
        if (request.HoldRadius > 0f && request.Cornered.Contains(agent))
            view.HoldFoes = mission.GetNearbyEnemyAgentCount(team, position, request.HoldRadius);
        if (main != null && request.Held.Contains(agent))
            view.TargetsPlayer = ReferenceEquals(agent.GetTargetAgent(), main);
        // IsRangedCached (bow or crossbow with ammunition; a thrown weapon does not count) is a managed read, so it
        // goes before the native ImmediateEnemy read.
        if (request.ArcherReadRange > 0f && view.DistanceToPlayer <= request.ArcherReadRange)
            view.IsIdleArcher = agent.IsAIControlled && ai != null && !ai.IsRetreating
                                && agent.IsRangedCached && agent.ImmediateEnemy == null;
        return view;
    }

    // DefaultBattleMissionAgentSpawnLogic is the spawner of field battles, sieges and sally-outs (the same
    // logic BattleEndLogic asks); with none, or no side, the answer is "unknown" and the cleanup stays off.
    private static int EnemyReserves(Mission mission, BattleSideEnum playerSide)
    {
        var spawn = mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
        if (spawn == null) return -1;
        switch (playerSide)
        {
            case BattleSideEnum.Attacker: return spawn.NumberOfRemainingDefenderTroops;
            case BattleSideEnum.Defender: return spawn.NumberOfRemainingAttackerTroops;
            default: return -1;
        }
    }

    private static Agent? Live(object handle) =>
        handle is Agent agent && AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive() ? agent : null;

    public bool StopRout(object handle)
    {
        var agent = Live(handle);
        var ai = agent?.CommonAIComponent;
        // A router already fading out is leaving the field for good; turning him now would only cut a fade short.
        if (agent == null || ai == null || !ai.IsRetreating || agent.IsFadingOut()) return false;
        MissionThreadGuard.NoteCall("FellFoesAdapter.StopRout", _warn);
        ai.StopRetreating();
        // StopRetreating lifts morale only to 0.02, which the next hit would break again; a rallied man gets the
        // morale a steady soldier recovers to (CommonAIComponent.RecoveryMorale).
        ai.Morale = Math.Max(ai.Morale, ai.RecoveryMorale);
        return true;
    }

    public bool Unsteady(object handle)
    {
        var agent = Live(handle);
        if (agent?.CommonAIComponent == null) return false;
        MissionThreadGuard.NoteCall("FellFoesAdapter.Unsteady", _warn);
        agent.SetMorale(0f);
        return true;
    }

    public bool SendOff(object handle)
    {
        var agent = Live(handle);
        var ai = agent?.CommonAIComponent;
        if (agent == null || ai == null || ai.IsRetreating) return false;
        MissionThreadGuard.NoteCall("FellFoesAdapter.SendOff", _warn);

        // The same sequence MissionAgentPanicHandler.OnPreMissionTick runs for a panicked agent (main thread).
        var formation = agent.Formation;
        formation?.OnBatchUnitRemovalStart();
        try
        {
            ai.Retreat();
            Mission.Current.OnAgentFleeing(agent);
        }
        finally
        {
            formation?.OnBatchUnitRemovalEnd();
        }

        // A sent-off soldier must be taken at the battle's end, not counted as a rout casualty or killed.
        agent.SetMorale(Math.Max(1f, agent.GetMorale()));
        return true;
    }

    public bool HoldTarget(object handle)
    {
        var agent = Live(handle);
        var main = Mission.Current?.MainAgent;
        if (agent == null || main == null || !AgentSlotIdentity.IsCurrentOccupant(main) || !main.IsActive()) return false;
        MissionThreadGuard.NoteCall("FellFoesAdapter.HoldTarget", _warn);
        agent.SetAutomaticTargetSelection(false);
        agent.SetTargetAgent(main);
        return true;
    }

    public void ReleaseTarget(object handle)
    {
        var agent = Live(handle);
        if (agent == null) return;
        MissionThreadGuard.NoteCall("FellFoesAdapter.ReleaseTarget", _warn);
        agent.SetAutomaticTargetSelection(true);
    }
}
