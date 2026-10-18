using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AnimationVariety.Hooks;

/// <summary>
/// Thin boundary for the gait variety feature: it hands each freshly built agent and the mission end to
/// <see cref="GaitVarietyService"/> and never throws. After the first exception it stops for the mission
/// with one warning; the battle itself goes on.
/// </summary>
public sealed class GaitVarietyMissionBehavior : MissionLogic
{
    private readonly GaitVarietyService _service;
    private readonly IModLogger _logger;
    private readonly Action<string> _warn;
    private bool _failed;

    public GaitVarietyMissionBehavior(GaitVarietyService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _warn = message => _logger.LogWarning(message);
    }

    // v1.5.5 caller: Mission.SpawnAgent runs `missionBehavior3.OnAgentBuild(agent, banner)` over every
    // behavior (Mission.cs:4390), after BuildAgent and before SpawnAgent returns, on the thread that called
    // SpawnAgent (the main thread: harmony-patches.md "Which thread runs your target"). The guard below is
    // the tripwire for a spawn from anywhere else; the engine write is skipped there, never parked, because
    // the soldier would already have played frames of the vanilla gait.
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_failed || agent == null) return;
        try
        {
            if (MissionThreadGuard.NoteCall("GaitVarietyMissionBehavior.OnAgentBuild", _warn)) return;
            _service.OnAgentBuild(new GaitVarietyAgent(agent));
        }
        catch (Exception ex)
        {
            Fail("OnAgentBuild", ex);
        }
    }

    // v1.5.5 caller: Mission.EndMissionInternal runs `missionBehavior.OnEndMissionInternal();` over every
    // behavior (Mission.cs:4649), before the agents are removed; MissionBehavior.cs:125-128 makes
    // OnEndMissionInternal call OnEndMission.
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
            _service.End();
        }
        catch (Exception ex)
        {
            Fail("EndService", ex);
        }
    }

    private void Fail(string site, Exception ex)
    {
        if (_failed) return;
        _failed = true;
        try { _logger.LogWarning($"[AnimVariety] gait {site} failed, gait variety off for this mission: {ex.GetType().Name}: {ex.Message}"); }
        catch { /* diagnostic only */ }
    }
}
