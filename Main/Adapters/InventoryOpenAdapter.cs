using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Reads the active <c>InventoryState</c>'s <c>InventoryLogic</c>. It casts the active state itself instead of
/// calling <c>InventoryScreenHelper.GetActiveInventoryState</c>, which raises a FailedAssert when another state
/// is active. The logic's private inventory mode is not read (no reflection): <c>IsTrading</c> separates the
/// trade screen from every other mode.
/// </summary>
public sealed class InventoryOpenAdapter : IInventoryOpenAdapter
{
    public bool TryRead(out string mode, out int leftCount, out int rightCount)
    {
        mode = "n/a";
        leftCount = -1;
        rightCount = -1;
        try
        {
            var logic = (GameStateManager.Current?.ActiveState as InventoryState)?.InventoryLogic;
            if (logic == null) return false;

            mode = logic.IsTrading ? "trade" : "inventory";
            leftCount = logic.GetElementCountOnSide(InventoryLogic.InventorySide.OtherInventory);
            rightCount = logic.GetElementCountOnSide(InventoryLogic.InventorySide.PlayerInventory);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
