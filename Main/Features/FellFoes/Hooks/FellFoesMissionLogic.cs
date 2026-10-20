using System;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.FellFoes.Hooks;

/// <summary>
/// Entry point of Fell Foes (#893, docs/features/fell-foes.md): ticks the per-mission service from
/// <c>OnMissionTick</c>, the one place every read and write runs (main thread, never overlapping the agent
/// tick). No event subscription, no Harmony patch. A fresh instance, and so fresh state, is built for every
/// mission. The service owns the once-a-second throttle and the mission gate. Every held archer is given his
/// automatic target choice back when the gate closes, at mission end, when the behavior is removed, and on failure.
///
/// Lifecycle (v1.5.5): <c>Mission.EndMissionInternal</c> runs <c>OnEndMissionInternal</c> over every
/// behavior (which calls <see cref="OnEndMission"/>) before the agents are removed, and
/// <c>RemoveMissionBehavior</c> runs <c>OnRemoveBehavior</c>, so a teardown that skips the first still ends here.
/// </summary>
public sealed class FellFoesMissionLogic : MissionLogic
{
    private readonly FellFoesService _service;
    private readonly IModLogger _logger;

    private bool _disabled;

    public FellFoesMissionLogic(FellFoesService service, IModLogger logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled) return;

        try
        {
            _service.Tick(dt);
        }
        catch (Exception ex)
        {
            // Never take a battle down over a tweak: this mission runs as vanilla from here.
            _disabled = true;
            _logger.LogError($"[FellFoes] disabled for this mission after {ex.GetType().Name}: {ex.Message}");
            Release();
        }
    }

    protected override void OnEndMission() => Release();

    public override void OnRemoveBehavior()
    {
        Release();
        base.OnRemoveBehavior();
    }

    private void Release()
    {
        try
        {
            _service.ReleaseHolds();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FellFoes] release at mission end failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
