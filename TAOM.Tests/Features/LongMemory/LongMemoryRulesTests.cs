using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongMemory;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// The arithmetic of Long Memory: a relation loss is a whole negative number, rounded half away from zero, capped at the
/// engine's relation floor, and a value that is not a finite number costs nothing.
/// </summary>
[TestClass]
public class LongMemoryRulesTests
{
    [DataTestMethod]
    [DataRow(50, 1f, 1f, -50)]
    [DataRow(50, 0.5f, 1f, -25)]
    [DataRow(50, 0.1f, 1f, -5)]
    [DataRow(50, 1f, 0.25f, -13)]    // 12.5 rounds away from zero
    [DataRow(25, 0.1f, 1f, -3)]      // 2.5 rounds away from zero
    [DataRow(1, 0.5f, 1f, -1)]       // 0.5 rounds to 1, not to 0
    [DataRow(1, 0.49f, 1f, 0)]
    [DataRow(50, 0.7f, 1f, -35)]     // 35 in decimal, not 34.99999 in float
    [DataRow(20, 1f, 0.6f, -12)]
    [DataRow(100, 5f, 1f, -100)]     // the engine's relation floor is -100
    [DataRow(int.MaxValue, 5f, 1f, -100)]
    public void Penalty_IsAWholeNegativeNumber_RoundedHalfAwayFromZero(int magnitude, float multiplier, float scale, int expected)
    {
        Assert.AreEqual(expected, LongMemoryRules.Penalty(magnitude, multiplier, scale));
    }

    [DataTestMethod]
    [DataRow(0, 1f, 1f)]
    [DataRow(-5, 1f, 1f)]              // a negative magnitude is never a gain
    [DataRow(int.MinValue, 1f, 1f)]
    [DataRow(50, 0f, 1f)]
    [DataRow(50, -1f, 1f)]             // a negative multiplier is never a gain
    [DataRow(50, 1f, 0f)]
    [DataRow(50, 1f, -1f)]
    public void Penalty_NothingToLoseOrASignFlip_CostsNothing(int magnitude, float multiplier, float scale)
    {
        Assert.AreEqual(0, LongMemoryRules.Penalty(magnitude, multiplier, scale));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(float.NegativeInfinity, 1f)]
    [DataRow(1f, float.NaN)]
    [DataRow(1f, float.PositiveInfinity)]
    [DataRow(1f, float.NegativeInfinity)]
    public void Penalty_ANonFiniteMultiplierOrScale_CostsNothing(float multiplier, float scale)
    {
        Assert.AreEqual(0, LongMemoryRules.Penalty(50, multiplier, scale));
    }

    [TestMethod]
    public void Penalty_AHugeFiniteMultiplier_DoesNotOverflow_AndStopsAtTheFloor()
    {
        Assert.AreEqual(-100, LongMemoryRules.Penalty(50, float.MaxValue));
        Assert.AreEqual(-100, LongMemoryRules.Penalty(50, 1f, float.MaxValue));
    }

    [TestMethod]
    public void Penalty_TheScaleDefaultsToOne()
    {
        Assert.AreEqual(-20, LongMemoryRules.Penalty(20, 1f));
    }

    [TestMethod]
    public void IsNoResistCoercionOption_TheTwoPinnedIds_AreRecognised()
    {
        Assert.AreEqual("force_supplies_no_resist_continue", LongMemoryRules.SuppliesNoResistOptionId);
        Assert.AreEqual("force_volunteers_no_resist_continue", LongMemoryRules.VolunteersNoResistOptionId);
        Assert.IsTrue(LongMemoryRules.IsNoResistCoercionOption("force_supplies_no_resist_continue"));
        Assert.IsTrue(LongMemoryRules.IsNoResistCoercionOption("force_volunteers_no_resist_continue"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("force_supplies_village_continue")]       // the shared "Continue" after a fight: the event already paid
    [DataRow("force_supplies_resist_continue")]        // a fight follows: the event pays
    [DataRow("force_volunteers_resist_continue")]
    [DataRow("force_supplies_no_resist_leave")]        // "Forget it": nothing was taken
    [DataRow("force_volunteers_no_resist_leave")]
    [DataRow("raid_village_no_resist_continue")]       // a raid pays on its own event
    [DataRow("FORCE_SUPPLIES_NO_RESIST_CONTINUE")]
    public void IsNoResistCoercionOption_AnyOtherId_IsNot(string? optionId)
    {
        Assert.IsFalse(LongMemoryRules.IsNoResistCoercionOption(optionId));
    }
}
