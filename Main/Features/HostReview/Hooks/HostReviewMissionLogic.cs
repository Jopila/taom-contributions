using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// Marks a mission as a Host Review and makes it harmless: no friendly fire and nobody dies. Reinforcements
/// cannot happen: the lineup is capped at the spawn logic's own battle size and the attacker side is empty.
/// Every callback here runs on the main thread, and the logic keeps no per-agent state.
/// </summary>
public sealed class HostReviewMissionLogic : MissionLogic
{
    private readonly IModLogger _logger;

    public HostReviewMissionLogic(IModLogger logger)
    {
        _logger = logger;
    }

    /// <summary>True when the mission carries this logic, which is how the order provider tells a Host Review apart.</summary>
    public static bool IsActiveIn(Mission? mission) =>
        mission != null && mission.GetMissionBehavior<HostReviewMissionLogic>() != null;

    public override void AfterStart()
    {
        Mission.ForceNoFriendlyFire = true;
        base.AfterStart();
        try
        {
            var message = new TextObject("{=taom_fcamp_review_start}Your host stands for review. No one can be hurt. Hold {LEAVE_KEY} to leave.");
            message.SetTextVariable("LEAVE_KEY", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("Generic", 4)));
            MBInformationManager.AddQuickInformation(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[HostReview] Start message failed: {ex.Message}");
        }
    }

    public override void OnAfterDeploymentFinished()
    {
        base.OnAfterDeploymentFinished();
        // FinishDeployment resets DisableDying to false just before this callback, so it is set here.
        Mission.DisableDying = true;
    }
}
