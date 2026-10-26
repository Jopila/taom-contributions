using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ManyHammers;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// The pure rules behind Many Hammers: how many units a held key asks for, how many of them the stamina and the
/// materials actually allow, and which smelting rows a lock hides. Slot indices are the engine's CraftingMaterials:
/// 0 IronOre, 1 Iron1 ... 6 Iron6, 7 Wood, 8 Charcoal.
/// </summary>
[TestClass]
public class BulkCraftRulesTests
{
    private const int Iron1 = 1;
    private const int Wood = 7;
    private const int Charcoal = 8;

    private static int[] Slots(params (int Slot, int Value)[] values)
    {
        var slots = new int[UnitSnapshot.Slots];
        foreach (var (slot, value) in values)
            slots[slot] = value;
        return slots;
    }

    // One weapon: gives one Iron1, takes one charcoal.
    private static UnitSnapshot Smelt(int stamina, int cost, int charcoal, int available) =>
        new(stamina, cost, Slots((Iron1, 1), (Charcoal, -1)), Slots((Charcoal, charcoal)), available);

    [TestMethod]
    public void ResolveCount_NoModifier_IsOne() =>
        Assert.AreEqual(1, BulkCraftRules.ResolveCount(true, false, false, 40));

    [TestMethod]
    public void ResolveCount_Shift_IsFive() =>
        Assert.AreEqual(5, BulkCraftRules.ResolveCount(true, true, false, 40));

    [TestMethod]
    public void ResolveCount_Ctrl_IsTheWholeStack() =>
        Assert.AreEqual(40, BulkCraftRules.ResolveCount(true, false, true, 40));

    [TestMethod]
    public void ResolveCount_BothHeld_CtrlWins() =>
        Assert.AreEqual(40, BulkCraftRules.ResolveCount(true, true, true, 40));

    [TestMethod]
    public void ResolveCount_Disabled_IsOneWhateverIsHeld()
    {
        Assert.AreEqual(1, BulkCraftRules.ResolveCount(false, true, false, 40));
        Assert.AreEqual(1, BulkCraftRules.ResolveCount(false, false, true, 40));
        Assert.AreEqual(1, BulkCraftRules.ResolveCount(false, true, true, 40));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    [DataRow(int.MinValue)]
    public void ResolveCount_CtrlOnAnEmptyOrNegativeStack_IsStillOne(int stack) =>
        Assert.AreEqual(1, BulkCraftRules.ResolveCount(true, false, true, stack));

    [TestMethod]
    public void ResolveCount_CtrlOnAnUnboundedStack_DoesNotOverflow() =>
        Assert.AreEqual(int.MaxValue, BulkCraftRules.ResolveCount(true, false, true, int.MaxValue));

    [TestMethod]
    public void PlanUnits_StopsWhenStaminaFallsToTheReserve()
    {
        // 40 -> 30 -> 20 -> 10: the unit at 10 is refused, a unit needs more than 10 stamina.
        Assert.AreEqual(3, BulkCraftRules.PlanUnits(99, Smelt(stamina: 40, cost: 10, charcoal: 99, available: 99)));
    }

    [TestMethod]
    public void PlanUnits_StopsWhenStaminaCannotPayTheNextCost()
    {
        // 30 > 10 and covers 25, then 5 does not.
        Assert.AreEqual(1, BulkCraftRules.PlanUnits(99, Smelt(stamina: 30, cost: 25, charcoal: 99, available: 99)));
    }

    [DataTestMethod]
    [DataRow(10)]
    [DataRow(0)]
    [DataRow(-4)]
    public void PlanUnits_StaminaAtOrBelowTheReserve_IsNone(int stamina) =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(5, Smelt(stamina, cost: 1, charcoal: 99, available: 99)));

    [TestMethod]
    public void PlanUnits_StaminaAboveTheReserveButBelowTheCost_IsNone() =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(5, Smelt(stamina: 15, cost: 20, charcoal: 99, available: 99)));

    [TestMethod]
    public void PlanUnits_StopsWhenCharcoalRunsOut() =>
        Assert.AreEqual(3, BulkCraftRules.PlanUnits(10, Smelt(stamina: 500, cost: 1, charcoal: 3, available: 99)));

    [TestMethod]
    public void PlanUnits_NoCharcoal_IsNone() =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(10, Smelt(stamina: 500, cost: 1, charcoal: 0, available: 99)));

    [TestMethod]
    public void PlanUnits_IsBoundedByTheStack() =>
        Assert.AreEqual(4, BulkCraftRules.PlanUnits(10, Smelt(stamina: 500, cost: 1, charcoal: 99, available: 4)));

    [TestMethod]
    public void PlanUnits_IsBoundedByWhatWasAskedFor() =>
        Assert.AreEqual(5, BulkCraftRules.PlanUnits(5, Smelt(stamina: 500, cost: 1, charcoal: 99, available: 99)));

    [TestMethod]
    public void PlanUnits_EmptyStack_IsNone() =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(5, Smelt(stamina: 500, cost: 1, charcoal: 99, available: 0)));

    [TestMethod]
    public void PlanUnits_RefiningStopsAtTheScarcerOfTwoInputs()
    {
        // Two wood and three charcoal per unit into one Iron1: wood 10 allows 5, charcoal 7 allows 2.
        var refine = new UnitSnapshot(500, 1,
            Slots((Wood, -2), (Charcoal, -3), (Iron1, 1)),
            Slots((Wood, 10), (Charcoal, 7)),
            int.MaxValue);

        Assert.AreEqual(2, BulkCraftRules.PlanUnits(50, refine));
    }

    [TestMethod]
    public void PlanUnits_AnOutputThatRefillsAnInput_CountsTowardTheStock()
    {
        // The formula takes two wood and gives one back: the snapshot carries the net, one wood a unit, so three
        // wood allows three units, not one.
        var refine = new UnitSnapshot(500, 1, Slots((Wood, -1)), Slots((Wood, 3)), int.MaxValue);

        Assert.AreEqual(3, BulkCraftRules.PlanUnits(50, refine));
    }

    [TestMethod]
    public void PlanUnits_ANetZeroSlot_NeverLimits()
    {
        var refine = new UnitSnapshot(500, 1, Slots((Wood, 0), (Iron1, 2)), Slots(), int.MaxValue);

        Assert.AreEqual(50, BulkCraftRules.PlanUnits(50, refine));
    }

    [TestMethod]
    public void PlanUnits_ZeroCostZeroInputsAndAnUnboundedSource_HitTheCap()
    {
        var free = new UnitSnapshot(11, 0, Slots((Iron1, 1)), Slots(), int.MaxValue);

        Assert.AreEqual(BulkCraftRules.MaxUnits, BulkCraftRules.PlanUnits(int.MaxValue, free));
        Assert.AreEqual(999, BulkCraftRules.MaxUnits);
    }

    [TestMethod]
    public void PlanUnits_ANegativeCost_CountsAsZero()
    {
        // A cost of -100 must not feed stamina back or block a unit: stamina stays 12, above the reserve, and all five run.
        Assert.AreEqual(5, BulkCraftRules.PlanUnits(5, Smelt(stamina: 12, cost: -100, charcoal: 99, available: 99)));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void PlanUnits_NothingAskedFor_IsNone(int desired) =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(desired, Smelt(stamina: 500, cost: 1, charcoal: 99, available: 99)));

    [TestMethod]
    public void PlanUnits_NoSnapshot_IsNone() =>
        Assert.AreEqual(0, BulkCraftRules.PlanUnits(5, null));

    [TestMethod]
    public void PlanUnits_HugeStockAndDelta_DoNotOverflow()
    {
        var big = new UnitSnapshot(500, 1, Slots((Iron1, int.MaxValue), (Charcoal, -1)), Slots((Iron1, int.MaxValue), (Charcoal, 99)), 99);

        Assert.AreEqual(99, BulkCraftRules.PlanUnits(99, big));
    }

    [TestMethod]
    public void PlanUnits_DoesNotChangeTheSnapshot()
    {
        var unit = Smelt(stamina: 500, cost: 1, charcoal: 3, available: 99);

        Assert.AreEqual(3, BulkCraftRules.PlanUnits(10, unit));
        Assert.AreEqual(3, BulkCraftRules.PlanUnits(10, unit));
        Assert.AreEqual(3, unit.Stock[Charcoal]);
        Assert.AreEqual(500, unit.Stamina);
    }

    [TestMethod]
    public void Snapshot_MissingOrShortArrays_ReadAsZeros()
    {
        var unit = new UnitSnapshot(50, 1, null, new[] { 4, 5 }, -7);

        Assert.AreEqual(UnitSnapshot.Slots, unit.Delta.Length);
        Assert.AreEqual(UnitSnapshot.Slots, unit.Stock.Length);
        Assert.AreEqual(5, unit.Stock[1]);
        Assert.AreEqual(0, unit.Stock[2]);
        Assert.AreEqual(0, unit.Available, "a negative supply reads as none");
    }

    [TestMethod]
    public void Snapshot_LongArrays_AreCutToNineSlots_AndCopied()
    {
        var source = new int[12];
        source[0] = 3;
        var unit = new UnitSnapshot(50, 1, source, source, 5);
        source[0] = 99;

        Assert.AreEqual(UnitSnapshot.Slots, unit.Delta.Length);
        Assert.AreEqual(3, unit.Delta[0], "the snapshot owns its numbers");
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_AnUnlockedSelection_IsLeftAloneWhenSomeRowsGo()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { false, true, false, true, false }, hasSelection: true, selectionIsLocked: false);

        Assert.AreEqual(SelectionChange.Unchanged, result.Selection);
        Assert.AreEqual(-1, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_ALockedSelection_MovesToTheFirstRemainingRow()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { true, true, false, false }, hasSelection: true, selectionIsLocked: true);

        Assert.AreEqual(SelectionChange.Reselect, result.Selection);
        Assert.AreEqual(2, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_ALockedSelectionAndNothingLeft_ClearsIt()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { true, true }, hasSelection: true, selectionIsLocked: true);

        Assert.AreEqual(SelectionChange.Clear, result.Selection);
        Assert.AreEqual(-1, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_AnUnlockedSelection_IsLeftAloneEvenWhenRowsGo()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { true, false }, hasSelection: true, selectionIsLocked: false);

        Assert.AreEqual(SelectionChange.Unchanged, result.Selection);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_EmptyListAndNoSelection_ChangesNothing()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new bool[0], hasSelection: false, selectionIsLocked: false);

        Assert.AreEqual(SelectionChange.Unchanged, result.Selection);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_NullList_IsAnEmptyList()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(null!, hasSelection: true, selectionIsLocked: true);

        Assert.AreEqual(SelectionChange.Clear, result.Selection);
    }

    // #911 review (Codex HIGH, Lens 5 G1): the last unlocked weapon smelted, only locked rows rebuilt. Vanilla keeps the gone
    // row selected because its list was not empty; once the filter empties it, vanilla's reselect hands OnItemSelection a
    // null (SmeltingVM.cs:273-277, throws at :222). No row left must always mean no selection.
    [TestMethod]
    public void SelectionAfterHidingLocked_AnUnlockedSelectionAndEveryRowLocked_ClearsIt()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { true, true }, hasSelection: true, selectionIsLocked: false);

        Assert.AreEqual(SelectionChange.Clear, result.Selection);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_NoSelectionAndEveryRowLocked_ChangesNothing()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(new[] { true, true }, hasSelection: false, selectionIsLocked: false);

        Assert.AreEqual(SelectionChange.Unchanged, result.Selection);
    }

    // Lens 5 I1: vanilla reselects by item (SmeltingVM.cs:200-203, :275-276), so a locked "Fine" twin can be picked; the
    // rule keeps to the same weapon when one is left, so the next Ctrl click does not smelt a different stack.
    [TestMethod]
    public void SelectionAfterHidingLocked_ALockedSelection_PrefersARowOfTheSameItem()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(
            new[] { false, true, false }, hasSelection: true, selectionIsLocked: true, sameItem: new[] { false, true, true });

        Assert.AreEqual(SelectionChange.Reselect, result.Selection);
        Assert.AreEqual(2, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_ALockedSelectionWithNoSameItemLeft_TakesTheFirstRow()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(
            new[] { false, true, false }, hasSelection: true, selectionIsLocked: true, sameItem: new[] { false, true, false });

        Assert.AreEqual(0, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_AShortSameItemList_ReadsFalsePastItsEnd()
    {
        // Row 2 is the only unlocked row of the same item, but the flags stop at row 1: it is not found, so the first row wins.
        var result = BulkCraftRules.SelectionAfterHidingLocked(
            new[] { false, true, false }, hasSelection: true, selectionIsLocked: true, sameItem: new[] { false, true });

        Assert.AreEqual(SelectionChange.Reselect, result.Selection);
        Assert.AreEqual(0, result.ReselectIndex);
    }

    [TestMethod]
    public void SelectionAfterHidingLocked_AShortSameItemList_StillFindsAMatchInsideItsRange()
    {
        var result = BulkCraftRules.SelectionAfterHidingLocked(
            new[] { false, false, false }, hasSelection: true, selectionIsLocked: true, sameItem: new[] { false, true });

        Assert.AreEqual(1, result.ReselectIndex);
    }
}
