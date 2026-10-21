using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.GoodDeeds;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>The rounding rule (half away from zero), pinned for every input the service can hand them.</summary>
[TestClass]
public class GoodDeedsRulesTests
{
    [DataTestMethod]
    [DataRow(5, 1f, 5)]
    [DataRow(5, 0.75f, 4)]   // 3.75
    [DataRow(2, 0.75f, 2)]   // 1.5 rounds up, away from zero
    [DataRow(5, 0.5f, 3)]    // 2.5 rounds up, away from zero
    [DataRow(2, 0.5f, 1)]
    [DataRow(1, 0.5f, 1)]    // 0.5 rounds to 1, not to 0
    [DataRow(1, 0.49f, 0)]
    [DataRow(5, 2f, 10)]
    [DataRow(5, 0f, 0)]
    [DataRow(0, 3f, 0)]
    [DataRow(5, 0.7f, 4)]    // 3.5 in decimal, not 3.4999999 in float
    [DataRow(-5, 0.5f, -3)]  // away from zero for a negative too
    [DataRow(20, 5f, 100)]
    public void Scale_RoundsHalfAwayFromZero(int amount, float multiplier, int expected)
    {
        Assert.AreEqual(expected, GoodDeedsRules.Scale(amount, multiplier));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Scale_ANonFiniteMultiplier_ScalesNothing(float multiplier)
    {
        Assert.AreEqual(0, GoodDeedsRules.Scale(5, multiplier));
    }

    [DataTestMethod]
    [DataRow(17.3f, 5, 2f, 45f)]    // (17.3 + 5) * 2 = 44.6
    [DataRow(17.3f, 5, 1f, 22f)]    // 22.3
    [DataRow(17.3f, 5, 0f, 0f)]
    [DataRow(0f, 5, 2f, 10f)]       // vanilla pays nothing when the captive already likes the owner
    [DataRow(0f, 5, 0.5f, 3f)]      // 2.5 rounds up
    [DataRow(40f, 5, 2f, 90f)]
    [DataRow(12f, 0, 1f, 12f)]
    public void ScaleDonation_AddsTheBonusThenScales(float vanilla, int bonus, float multiplier, float expected)
    {
        Assert.AreEqual(expected, GoodDeedsRules.ScaleDonation(vanilla, bonus, multiplier));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ScaleDonation_ANonFiniteVanillaGain_CountsAsZero(float vanilla)
    {
        Assert.AreEqual(10f, GoodDeedsRules.ScaleDonation(vanilla, 5, 2f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void ScaleDonation_ANonFiniteMultiplier_GivesNothing(float multiplier)
    {
        Assert.AreEqual(0f, GoodDeedsRules.ScaleDonation(17f, 5, multiplier));
    }
}
