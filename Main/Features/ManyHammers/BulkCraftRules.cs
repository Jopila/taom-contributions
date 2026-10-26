using System;
using System.Collections.Generic;

namespace TAOM.Features.ManyHammers;

/// <summary>
/// The pure rules of Many Hammers, over plain numbers: how many units a held key asks for, how many of them the stamina and
/// the materials allow, and what hiding the locked rows does to the smelting selection. Nothing here touches the engine; the adapters feed it a
/// <see cref="UnitSnapshot"/> and the hooks act on the answer.
/// </summary>
public static class BulkCraftRules
{
    /// <summary>Units asked for by Shift.</summary>
    public const int ShiftCount = 5;

    /// <summary>
    /// The most units one click plans. A cost-free feat or an unbounded refining formula would otherwise plan without
    /// end, and the loop that runs the plan needs a number to stop at.
    /// </summary>
    public const int MaxUnits = 999;

    /// <summary>A unit needs more stamina than this: the smithy's own button refuses at 10 or less (CraftingVM.HaveEnergy).</summary>
    public const int ReservedStamina = 10;

    /// <summary>
    /// How many units the player asked for. Ctrl wins over Shift and asks for the whole <paramref name="stack"/>, never less
    /// than one; Shift asks for <see cref="ShiftCount"/>; with the feature off, or neither key held, one.
    /// </summary>
    public static int ResolveCount(bool enabled, bool shift, bool ctrl, int stack)
    {
        if (!enabled) return 1;
        if (ctrl) return Math.Max(1, stack);
        return shift ? ShiftCount : 1;
    }

    /// <summary>
    /// How many of up to <paramref name="desired"/> units fit, simulated one at a time. A unit runs only while the stamina is
    /// above <see cref="ReservedStamina"/> and covers the unit's cost, and no material would go below zero; the source (the
    /// stack) and <see cref="MaxUnits"/> bound the count. A negative cost counts as none. The snapshot is not changed.
    /// </summary>
    public static int PlanUnits(int desired, UnitSnapshot? unit)
    {
        if (unit == null || desired <= 0) return 0;

        var limit = Math.Min(Math.Min(desired, unit.Available), MaxUnits);
        long stamina = unit.Stamina;
        long cost = Math.Max(0, unit.EnergyCost);
        var stock = new long[UnitSnapshot.Slots];
        for (var i = 0; i < stock.Length; i++)
            stock[i] = unit.Stock[i];

        var units = 0;
        while (units < limit)
        {
            if (!(stamina > ReservedStamina && stamina >= cost)) break;
            if (WouldGoNegative(stock, unit.Delta)) break;

            for (var i = 0; i < stock.Length; i++)
                stock[i] += unit.Delta[i];
            stamina -= cost;
            units++;
        }

        return units;
    }

    /// <summary>
    /// What happens to the selection once the rows flagged in <paramref name="locks"/> (one flag per row) are removed. When
    /// no row is left any selection clears, because vanilla's reselect after a smelt hands a null to its selection handler
    /// once the list is empty (SmeltingVM.cs:273-277). A selection that was locked moves to the first row left of the same
    /// item (<paramref name="sameItem"/>, one flag per row; a short list reads as false past its end), else the first row
    /// left. Any other selection stays, for vanilla's own same-item reselect.
    /// </summary>
    public static LockFilterResult SelectionAfterHidingLocked(IReadOnlyList<bool> locks, bool hasSelection, bool selectionIsLocked,
        IReadOnlyList<bool>? sameItem = null)
    {
        var first = -1;
        var firstSameItem = -1;
        if (locks != null)
        {
            for (var i = 0; i < locks.Count; i++)
            {
                if (locks[i]) continue;
                if (first < 0) first = i;
                if (firstSameItem < 0 && sameItem != null && i < sameItem.Count && sameItem[i]) firstSameItem = i;
            }
        }

        if (first < 0)
            return new LockFilterResult(hasSelection ? SelectionChange.Clear : SelectionChange.Unchanged, -1);
        if (!selectionIsLocked)
            return new LockFilterResult(SelectionChange.Unchanged, -1);

        return new LockFilterResult(SelectionChange.Reselect, firstSameItem >= 0 ? firstSameItem : first);
    }

    private static bool WouldGoNegative(long[] stock, int[] delta)
    {
        for (var i = 0; i < stock.Length; i++)
            if (stock[i] + delta[i] < 0) return true;
        return false;
    }
}
