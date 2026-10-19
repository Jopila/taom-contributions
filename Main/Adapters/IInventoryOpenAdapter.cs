namespace TAOM.Adapters;

/// <summary>Read-only view of the inventory screen being opened, for the [ScreenOpen] line's detail (ADR-007).</summary>
public interface IInventoryOpenAdapter
{
    /// <summary>
    /// The mode ("trade" or "inventory"), the other party's element count and the player's element count of
    /// the active inventory state. False when no inventory state is active or it cannot be read.
    /// </summary>
    bool TryRead(out string mode, out int leftCount, out int rightCount);
}
