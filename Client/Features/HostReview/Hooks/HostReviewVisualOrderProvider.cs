using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.VisualOrders;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// The vanilla order list, offered in a Host Review mission. Vanilla's own provider is available only
/// when the mission is not friendly, and this mission stays friendly so that the leave key can end it; the
/// whole order set (both layouts) is inherited unchanged and only the availability differs.
/// </summary>
public sealed class HostReviewVisualOrderProvider : DefaultVisualOrderProvider
{
    public override bool IsAvailable() => HostReviewMissionLogic.IsActiveIn(Mission.Current);
}
