using SandBox.View.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// The view list for the "TaomHostReview" mission. It is the campaign field battle's list
/// (SandBoxMissionViews.OpenBattleMission) with these views left out:
/// the battle scoreboard (it marks the mission as not friendly, which would take away holding the leave key to leave
/// and hide the order bar's friendly-mission gate), the equipment drop view, MissionSingleplayerViewHandler
/// (the campaign hotkey panels), the battle music, the kill notification feed, MissionCampaignBattleSpectatorView,
/// the order-of-battle screen, and MissionPreloadView (it reads MapEvent.PlayerMapEvent unguarded). The face
/// cache view is kept. The engine finds this class by its attribute in every loaded module assembly
/// (ViewCreatorManager scans the assemblies that reference the View assembly).
/// </summary>
// Every static method on a [ViewCreatorModule] class must carry [ViewMethod]: ViewCreatorManager reads attribute [0]
// unguarded, and an exception there breaks every mission view (ViewCreatorManager.cs:63-66).
[ViewCreatorModule]
public static class HostReviewMissionViews
{
    public const string MissionName = "TaomHostReview";

    [ViewMethod(MissionName)]
    public static MissionView[] OpenHostReviewViews(Mission mission)
    {
        return new MissionView[]
        {
            new MissionCampaignView(),
            ViewCreator.CreateMissionSingleplayerEscapeMenu(CampaignOptions.IsIronmanMode),
            ViewCreator.CreateMissionAgentLabelUIHandler(mission),
            ViewCreator.CreateOptionsUIHandler(),
            ViewCreator.CreateMissionOrderUIHandler(),
            new OrderTroopPlacer(null),
            ViewCreator.CreateMissionAgentStatusUIHandler(mission),
            ViewCreator.CreateMissionMainAgentEquipmentController(mission),
            ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
            ViewCreator.CreateMissionAgentLockVisualizerView(mission),
            new DeploymentMissionView(),
            new MissionDeploymentBoundaryMarker("swallowtail_banner", 2f),
            ViewCreator.CreateMissionBoundaryCrossingView(),
            new MissionBoundaryWallView(),
            ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
            new MissionFormationTargetSelectionHandler(),
            ViewCreator.CreateMissionSpectatorControlView(mission),
            new MissionItemContourControllerView(),
            new MissionAgentContourControllerView(),
            ViewCreator.CreatePhotoModeView(),
            new MissionFaceCacheView(),
        };
    }
}
