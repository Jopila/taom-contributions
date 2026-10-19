using System.Collections.Generic;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Adapters;

/// <summary>
/// A lord's three equipment sets. A set that is the engine's shared fallback (a hero without his own copy
/// holds a reference to one object every such hero uses) is never read, written or re-kitted.
/// </summary>
public interface ILordEquipmentAdapter
{
    /// <summary>Every non-empty piece in the hero's own sets.</summary>
    IReadOnlyList<LordEquipmentPiece> ReadPieces(string heroId);

    bool ClearSlot(string heroId, LordEquipmentSet set, int slot);

    /// <summary>True when the slot of the hero's own set is empty.</summary>
    bool IsSlotEmpty(string heroId, LordEquipmentSet set, int slot);

    /// <summary>Puts an item without a modifier into the slot, replacing what is there; false when the item or set is unavailable.</summary>
    bool SetItem(string heroId, LordEquipmentSet set, int slot, string itemId);

    /// <summary>
    /// Gives the lord a fresh battle and civilian kit from the engine's coming-of-age selection, slot by slot into
    /// his own sets: only slots that are empty or hold <paramref name="ragItemId"/>, so a piece he kept stays.
    /// The stealth set is not refilled.
    /// </summary>
    bool GiveFreshKit(string heroId, string ragItemId);
}
