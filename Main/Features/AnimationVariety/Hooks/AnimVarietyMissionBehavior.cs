using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AnimationVariety.Hooks;

/// <summary>
/// Thin boundary for the <c>[AnimVariety]</c> probe: it hands the human agents, the hits and the mission
/// end to <see cref="AnimVarietyService"/> and never throws. After the first exception it stops for the
/// mission with one warning. A mission with the probe switched off, or with no alternative action codes,
/// costs one flag check per tick.
/// </summary>
public sealed class AnimVarietyMissionBehavior : MissionLogic
{
    private readonly AnimVarietyService _service;
    private readonly IModLogger _logger;
    private readonly Func<IReadOnlyList<IAnimVarietyAgent>> _snapshot;
    private readonly List<IAnimVarietyAgent> _agents = new();

    // Native raises OnAgentHit on a thread of its choosing (harmony-patches.md "Which thread runs your
    // target"), so the service write is replayed from OnMissionTick when it arrives elsewhere.
    private readonly DeferredCallbackQueue _deferred;
    private bool _failed;

    public AnimVarietyMissionBehavior(AnimVarietyService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _snapshot = HumanAgents;
        _deferred = new DeferredCallbackQueue(message => _logger.LogWarning(message));
    }

    // v1.5.5 caller: `MissionBehaviors[i].OnMissionTick(dt)` in Mission.OnTick, main thread.
    public override void OnMissionTick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        if (_failed) return;
        try
        {
            _deferred.Drain();
            _service.Tick(dt, _snapshot);
            // The wrappers hold Agent references; drop them now rather than keep them until the next tick.
            _agents.Clear();
        }
        catch (Exception ex)
        {
            Fail("OnMissionTick", ex);
            _agents.Clear();
        }
    }

    // v1.5.5 caller: `missionBehavior.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in b,
    // in collisionData)` (Mission.cs:5670). The attacker's channel-1 values are read where the hit
    // arrives, so they describe the swing that hit; only the write into the service is deferred. A missile
    // blow returns before any allocation: the projectile lands long after the swing, so channel 1 would
    // name a later one (the service ignores a missile too, so a later caller cannot forget). This method
    // holds no lambda: the compiler allocates a closure on entry to the scope that declares a captured
    // variable, which for a method local or parameter is method entry, before the early return; so the
    // capture lives in RecordMeleeHit.
    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
        in Blow blow, in AttackCollisionData attackCollisionData)
    {
        if (_failed || blow.IsMissile || affectorAgent == null || !_service.IsTracking) return;
        RecordMeleeHit(affectorAgent);
    }

    private void RecordMeleeHit(Agent affectorAgent)
    {
        try
        {
            var attacker = new AnimVarietyAgent(affectorAgent);
            if (!attacker.IsActive) return;
            var handle = attacker.Handle;
            var actionName = attacker.Channel1ActionName;
            var progress = attacker.Channel1Progress;
            _deferred.RunOrDefer("AnimVarietyMissionBehavior.OnAgentHit",
                () => _service.RecordHit(handle, actionName, progress, false));
        }
        catch (Exception ex)
        {
            Fail("OnAgentHit", ex);
        }
    }

    // v1.5.5 caller: Mission.EndMissionInternal runs `missionBehavior.OnEndMissionInternal();` over every
    // behavior (Mission.cs:4649), before the agents are removed (:4652-4655); MissionBehavior.cs:125-128
    // makes OnEndMissionInternal call OnEndMission. EndMissionInternal is reached from Mission.cs:4871 and :4880.
    protected override void OnEndMission() => EndService();

    // v1.5.5 caller: `missionBehavior.OnRemoveBehavior();` in RemoveMissionBehavior (Mission.cs:4709). A
    // teardown that never reaches EndMissionInternal still ends here; after OnEndMission the service
    // ignores the second End.
    public override void OnRemoveBehavior()
    {
        EndService();
        base.OnRemoveBehavior();
    }

    private void EndService()
    {
        try
        {
            if (!_failed) _deferred.Drain();
            _service.End();
            _deferred.Clear();
        }
        catch (Exception ex)
        {
            Fail("EndService", ex);
        }
    }

    private IReadOnlyList<IAnimVarietyAgent> HumanAgents()
    {
        _agents.Clear();
        var agents = Mission?.Agents;
        if (agents == null) return _agents;
        foreach (var agent in agents)
            if (agent != null && agent.IsHuman) _agents.Add(new AnimVarietyAgent(agent));
        return _agents;
    }

    private void Fail(string site, Exception ex)
    {
        if (_failed) return;
        _failed = true;
        try { _logger.LogWarning($"[AnimVariety] {site} failed, probe off for this mission: {ex.GetType().Name}: {ex.Message}"); }
        catch { /* diagnostic only */ }
    }
}
