using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HarvestRoad;

namespace TAOM.Tests.Features.HarvestRoad;

/// <summary>
/// The roster diff behind Harvest Road (#912). A positive count is a line that left the roster (sold), a negative count a
/// line that arrived (bought), which is the sign the map's trade notice reads. Keys are strings here; the patches key it by
/// <c>EquipmentElement</c>, so two modifiers of one item stay two lines.
/// </summary>
[TestClass]
public class TradeNoticeDiffTests
{
    private static List<(string, int)> Roster(params (string, int)[] lines) => lines.ToList();

    private static List<(string Key, int Count)> Diff(List<(string, int)>? before, List<(string, int)>? after) =>
        TradeNoticeDiff.Compute(before, after);

    [TestMethod]
    public void Compute_UnchangedRoster_IsEmpty()
    {
        var roster = Roster(("grain", 40), ("cloth", 5));

        Assert.AreEqual(0, Diff(roster, Roster(("grain", 40), ("cloth", 5))).Count);
    }

    [TestMethod]
    public void Compute_PartialSale_IsPositiveForTheDifference()
    {
        var result = Diff(Roster(("grain", 40)), Roster(("grain", 15)));

        CollectionAssert.AreEqual(new[] { ("grain", 25) }, result);
    }

    [TestMethod]
    public void Compute_WholeStackSold_IsPositiveForTheWholeStack()
    {
        var result = Diff(Roster(("grain", 40), ("cloth", 5)), Roster(("cloth", 5)));

        CollectionAssert.AreEqual(new[] { ("grain", 40) }, result);
    }

    [TestMethod]
    public void Compute_NewItemBought_IsNegative()
    {
        var result = Diff(Roster(("cloth", 5)), Roster(("cloth", 5), ("grain", 12)));

        CollectionAssert.AreEqual(new[] { ("grain", -12) }, result);
    }

    [TestMethod]
    public void Compute_StackToppedUp_IsNegativeForTheIncrease()
    {
        var result = Diff(Roster(("grain", 10)), Roster(("grain", 18)));

        CollectionAssert.AreEqual(new[] { ("grain", -8) }, result);
    }

    [TestMethod]
    public void Compute_DuplicateKeys_AreSummedOnBothSides()
    {
        var result = Diff(Roster(("grain", 10), ("grain", 5)), Roster(("grain", 4), ("grain", 3)));

        CollectionAssert.AreEqual(new[] { ("grain", 8) }, result);
    }

    [TestMethod]
    public void Compute_LinesThatNetToZero_AreDropped()
    {
        var result = Diff(Roster(("grain", 10), ("cloth", 3)), Roster(("grain", 6), ("grain", 4), ("cloth", 1)));

        CollectionAssert.AreEqual(new[] { ("cloth", 2) }, result);
    }

    [TestMethod]
    public void Compute_ZeroCountLines_AreDropped()
    {
        Assert.AreEqual(0, Diff(Roster(("grain", 0)), Roster(("cloth", 0))).Count);
    }

    [TestMethod]
    public void Compute_NullBefore_IsEmpty_EvenWhenTheAfterRosterHasLines()
    {
        Assert.AreEqual(0, Diff(null, Roster(("grain", 40))).Count);
    }

    [TestMethod]
    public void Compute_NullAfter_IsEmpty_EvenWhenTheBeforeRosterHasLines()
    {
        Assert.AreEqual(0, Diff(Roster(("grain", 40)), null).Count);
    }

    [TestMethod]
    public void Compute_OrderIsStable_BeforeLinesInTheirOrder_ThenNewLinesInTheirOrder()
    {
        var result = Diff(
            Roster(("wine", 3), ("grain", 9), ("cloth", 4)),
            Roster(("salt", 2), ("wine", 1), ("cloth", 1), ("fish", 6)));

        CollectionAssert.AreEqual(new[] { ("wine", 2), ("grain", 9), ("cloth", 3), ("salt", -2), ("fish", -6) }, result);
    }

    [TestMethod]
    public void Compute_ModifiersAreKeptApart_WhenTheKeyCarriesTheModifier()
    {
        var result = Diff(Roster(("sword|fine", 2), ("sword|", 3)), Roster(("sword|", 3)));

        CollectionAssert.AreEqual(new[] { ("sword|fine", 2) }, result);
    }

    [TestMethod]
    public void Compute_SumsAreDoneInLong_SoTwoHugeCountsDoNotWrap()
    {
        var result = Diff(Roster(("grain", int.MaxValue), ("grain", int.MaxValue)), Roster(("grain", 1)));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(int.MaxValue, result[0].Count, "a delta past the int range is clamped, never wrapped to a negative");
    }
}
