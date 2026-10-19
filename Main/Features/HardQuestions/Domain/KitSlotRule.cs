namespace TAOM.Features.HardQuestions.Domain;

/// <summary>Which slots a fresh kit may fill when a stripped lord is released.</summary>
public static class KitSlotRule
{
    /// <summary>
    /// True when the kit has a piece for the slot and the lord holds nothing there or only the rag. A piece he
    /// kept (a named weapon or armour) is never overwritten, and a gap in the kit never empties a slot.
    /// </summary>
    public static bool CanFill(string? currentItemId, string? kitItemId, string ragItemId)
    {
        if (string.IsNullOrEmpty(kitItemId))
            return false;
        return string.IsNullOrEmpty(currentItemId) || currentItemId == ragItemId;
    }
}
