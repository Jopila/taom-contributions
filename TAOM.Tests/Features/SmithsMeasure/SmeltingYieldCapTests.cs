using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SmithsMeasure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// The smelt cap (#910) on plain arrays in the engine's slot order: iron ore, the six iron grades, hardwood, charcoal. Charcoal
/// holds the engine's own cost as a negative number, and the cap only ever lowers a positive value.
/// </summary>
[TestClass]
public class SmeltingYieldCapTests
{
    private const int Wood = 7;
    private const int Charcoal = 8;

    private static int[] Slots(params int[] values) => values;

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public void EachMetalSlot_IsCappedAtOne(int slot)
    {
        var output = new int[9];
        output[slot] = 4;

        var result = SmeltingYieldCap.Apply(output, 1);

        Assert.AreEqual(1, result[slot]);
    }

    [TestMethod]
    public void AMetalSlotAtOrUnderTheCap_IsLeftAlone()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 1, 2, 0, 0, 0, 0, 0, -1), 2);

        CollectionAssert.AreEqual(Slots(0, 1, 2, 0, 0, 0, 0, 0, -1), result);
    }

    [TestMethod]
    public void HardwoodAlone_IsCappedLikeMetal()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 0, 0, 0, 0, 0, 5, -1), 1);

        Assert.AreEqual(1, result[Wood]);
    }

    [TestMethod]
    public void HardwoodAlone_AtTheCap_IsKept()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 0, 0, 0, 0, 0, 1, -1), 1);

        Assert.AreEqual(1, result[Wood]);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    public void Hardwood_IsDropped_WhenAnyMetalSlotComesOut(int metalSlot)
    {
        var output = new int[9];
        output[metalSlot] = 2;
        output[Wood] = 3;
        output[Charcoal] = -1;

        var result = SmeltingYieldCap.Apply(output, 1);

        Assert.AreEqual(0, result[Wood]);
        Assert.AreEqual(1, result[metalSlot]);
    }

    [TestMethod]
    public void Hardwood_IsDropped_WhenTheMetalIsAlreadyUnderTheCap()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 1, 0, 0, 0, 0, 0, 1, -1), 1);

        Assert.AreEqual(0, result[Wood]);
    }

    [TestMethod]
    public void Charcoal_IsNeverChanged_PositiveOrNegative()
    {
        Assert.AreEqual(-1, SmeltingYieldCap.Apply(Slots(3, 0, 0, 0, 0, 0, 0, 0, -1), 1)[Charcoal]);
        Assert.AreEqual(-4, SmeltingYieldCap.Apply(Slots(3, 0, 0, 0, 0, 0, 0, 0, -4), 1)[Charcoal]);
        Assert.AreEqual(5, SmeltingYieldCap.Apply(Slots(3, 0, 0, 0, 0, 0, 0, 0, 5), 1)[Charcoal]);
    }

    [TestMethod]
    public void ANegativeValue_IsNeverRaisedOrZeroed()
    {
        var result = SmeltingYieldCap.Apply(Slots(-2, 0, 0, 0, 0, 0, 0, -3, -1), 1);

        CollectionAssert.AreEqual(Slots(-2, 0, 0, 0, 0, 0, 0, -3, -1), result);
    }

    [TestMethod]
    public void ANegativeHardwood_IsKept_EvenBesideMetal()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 3, 0, 0, 0, 0, -2, -1), 1);

        Assert.AreEqual(-2, result[Wood]);
        Assert.AreEqual(1, result[2]);
    }

    [TestMethod]
    public void ACapOfThree_LetsThreeOfAGradeThrough_AndKeepsHardwoodDropped()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 5, 3, 0, 0, 0, 4, -1), 3);

        CollectionAssert.AreEqual(Slots(0, 0, 3, 3, 0, 0, 0, 0, -1), result);
    }

    [TestMethod]
    public void ACapOfThree_CapsHardwoodAlone_AtThree()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 0, 0, 0, 0, 0, 9, -1), 3);

        Assert.AreEqual(3, result[Wood]);
    }

    [TestMethod]
    public void TheInputArray_IsNeverMutated()
    {
        var input = Slots(5, 5, 5, 5, 5, 5, 5, 5, -1);

        var result = SmeltingYieldCap.Apply(input, 1);

        CollectionAssert.AreEqual(Slots(5, 5, 5, 5, 5, 5, 5, 5, -1), input);
        Assert.AreNotSame(input, result);
    }

    [TestMethod]
    public void AShortArray_IsCappedWhereItHasSlots_AndNeverThrows()
    {
        var result = SmeltingYieldCap.Apply(Slots(4, 4, 4), 1);

        CollectionAssert.AreEqual(Slots(1, 1, 1), result);
    }

    [TestMethod]
    public void AnArrayEndingBeforeHardwood_HasNoHardwoodToDrop()
    {
        var result = SmeltingYieldCap.Apply(Slots(0, 0, 0, 0, 0, 0, 3), 1);

        CollectionAssert.AreEqual(Slots(0, 0, 0, 0, 0, 0, 1), result);
    }

    [TestMethod]
    public void AnEmptyArray_IsReturnedEmpty()
    {
        Assert.AreEqual(0, SmeltingYieldCap.Apply(new int[0], 1).Length);
    }

    [TestMethod]
    public void ANullArray_IsReturnedAsNull()
    {
        Assert.IsNull(SmeltingYieldCap.Apply(null!, 1));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void ACapUnderOne_ChangesNothing(int cap)
    {
        var input = Slots(5, 0, 0, 0, 0, 0, 0, 2, -1);

        CollectionAssert.AreEqual(Slots(5, 0, 0, 0, 0, 0, 0, 2, -1), SmeltingYieldCap.Apply(input, cap));
    }

    [TestMethod]
    public void ALongerArray_LeavesTheSlotsPastCharcoalAlone()
    {
        var result = SmeltingYieldCap.Apply(Slots(2, 0, 0, 0, 0, 0, 0, 0, -1, 9), 1);

        CollectionAssert.AreEqual(Slots(1, 0, 0, 0, 0, 0, 0, 0, -1, 9), result);
    }
}
