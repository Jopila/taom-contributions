using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>#846 phase 3: the hold, the fade step, the whole-hour conversion, the XP table and the "~" text. All integer or
/// plain-double arithmetic, so each row is exact.</summary>
[TestClass]
public class HiddenHostsMemoryMathTests
{
    [DataTestMethod]
    [DataRow(0, 6)]
    [DataRow(-40, 6)]
    [DataRow(150, 12)]
    [DataRow(300, 18)]
    [DataRow(1, 7)]
    [DataRow(1000, 46)]
    [DataRow(5000, 46)]
    [DataRow(int.MinValue, 6)]
    [DataRow(int.MaxValue, 46)]
    public void HoldHours_IsSixTimesOnePlusScoutOver150_RoundedUp_AndClamped(int scout, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.HoldHours(scout));

    [DataTestMethod]
    [DataRow(0, 8)]
    [DataRow(-40, 8)]
    [DataRow(150, 12)]
    [DataRow(300, 16)]
    [DataRow(1, 9)]
    [DataRow(1000, 35)]
    [DataRow(5000, 35)]
    [DataRow(int.MinValue, 8)]
    [DataRow(int.MaxValue, 35)]
    public void FadeStepHours_IsEightTimesOnePlusScoutOver300_RoundedUp_AndClamped(int scout, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.FadeStepHours(scout));

    [DataTestMethod]
    [DataRow(0.0, 0)]
    [DataRow(0.99, 0)]
    [DataRow(1.0, 1)]
    [DataRow(2184576.7, 2184576)]
    [DataRow(2147483647.0, 2147483647)]
    public void ToWholeHours_FloorsAFiniteInRangeValue(double hours, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.ToWholeHours(hours));

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.5)]
    [DataRow(-1e9)]
    [DataRow(2147483648.0)]
    [DataRow(1e300)]
    public void ToWholeHours_NotFiniteNegativeOrPastInt_IsMinusOne(double hours) =>
        Assert.AreEqual(-1, HiddenHostsMath.ToWholeHours(hours));

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(1, 20)]
    [DataRow(2, 30)]
    [DataRow(3, 50)]
    [DataRow(4, 0)]
    [DataRow(int.MaxValue, 0)]
    public void XpForStage_IsTwentyThirtyFifty_AndNothingElse(int stage, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.XpForStage(stage));

    [TestMethod]
    public void FormatMemory_PutsTheTildeInFrontOfTheMask()
    {
        Assert.AreEqual("~1??", HiddenHostsMath.FormatMemory(150, 1));
        Assert.AreEqual("~15?", HiddenHostsMath.FormatMemory(150, 2));
        Assert.AreEqual("~150", HiddenHostsMath.FormatMemory(150, 3));
        Assert.AreEqual("~???", HiddenHostsMath.FormatMemory(150, 0));
        Assert.AreEqual("~", HiddenHostsMath.MemoryPrefix);
    }

    [DataTestMethod]
    [DataRow(123, 0, 0)]
    [DataRow(123, 1, 1)]
    [DataRow(123, 2, 12)]
    [DataRow(123, 3, 123)]
    [DataRow(123, 9, 123)]
    [DataRow(123, -2, 0)]
    [DataRow(5, 1, 5)]
    [DataRow(0, 1, 0)]
    [DataRow(-5, 1, 0)]
    [DataRow(1500, 2, 15)]
    [DataRow(int.MaxValue, 3, 214)]
    [DataRow(int.MaxValue, 10, int.MaxValue)]
    public void LeadingDigits_IsThePrefixOfTheDecimalCount(int count, int known, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.LeadingDigits(count, known));

    [DataTestMethod]
    [DataRow(150, 2, 155, true)]
    [DataRow(150, 2, 160, false)]
    [DataRow(150, 0, 999, true)]
    [DataRow(150, 0, 99, false)]
    [DataRow(150, 0, 1000, false)]
    [DataRow(1500, 1, 123, false)]
    [DataRow(123, 3, 123, true)]
    [DataRow(123, 3, 124, false)]
    [DataRow(123, 9, 123, true)]
    [DataRow(7, 1, 7, true)]
    [DataRow(7, 1, 8, false)]
    [DataRow(7, 0, 80, false)]
    [DataRow(0, 1, 5, false)]
    [DataRow(0, 1, 0, true)]
    public void Agrees_SameDigitCountAndSameLeadingDigits(int count, int known, int remembered, bool expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.Agrees(count, known, remembered));
}
