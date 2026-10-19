using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>#846: the pure arithmetic behind the nameplate mask. Every threshold row is pinned just inside and just outside.</summary>
[TestClass]
public class HiddenHostsMathTests
{
    private const float Eps = 0.0001f;

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(-5, 1)]
    [DataRow(9, 1)]
    [DataRow(10, 2)]
    [DataRow(99, 2)]
    [DataRow(100, 3)]
    [DataRow(999, 3)]
    [DataRow(1000, 4)]
    [DataRow(9999, 4)]
    [DataRow(10000, 5)]
    [DataRow(99999, 5)]
    [DataRow(100000, 6)]
    [DataRow(int.MaxValue, 10)]
    public void DigitCount_CountsDecimalDigits_ZeroIsOne(int count, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.DigitCount(count));

    [DataTestMethod]
    [DataRow(1, 0, 0.28f)]
    [DataRow(2, 0, 0.50f)]
    [DataRow(2, 1, 0.26f)]
    [DataRow(3, 0, 0.62f)]
    [DataRow(3, 1, 0.32f)]
    [DataRow(3, 2, 0.20f)]
    [DataRow(4, 0, 0.70f)]
    [DataRow(4, 1, 0.45f)]
    [DataRow(4, 2, 0.28f)]
    [DataRow(4, 3, 0.16f)]
    [DataRow(5, 0, 0.75f)]
    [DataRow(5, 1, 0.50f)]
    [DataRow(5, 2, 0.32f)]
    [DataRow(5, 3, 0.20f)]
    [DataRow(5, 4, 0.14f)]
    [DataRow(6, 0, 0.75f)]
    [DataRow(6, 4, 0.14f)]
    [DataRow(6, 5, 0.14f)]
    [DataRow(9, 8, 0.14f)]
    public void Threshold_FollowsTheRowForTheDigitCount_ExtraDigitsReuseTheLast(int digits, int index, float expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.Threshold(digits, index), Eps);

    [TestMethod]
    public void ScoutMultiplier_NoSkill_NotSameRealm_IsOne() =>
        Assert.AreEqual(1f, HiddenHostsMath.ScoutMultiplier(0, false), Eps);

    [TestMethod]
    public void ScoutMultiplier_SameRealm_Doubles() =>
        Assert.AreEqual(2f, HiddenHostsMath.ScoutMultiplier(0, true), Eps);

    [TestMethod]
    public void ScoutMultiplier_NegativeSkill_CountsAsZero() =>
        Assert.AreEqual(1f, HiddenHostsMath.ScoutMultiplier(-80, false), Eps);

    [TestMethod]
    public void ScoutMultiplier_Skill50_AddsTwelvePercent() =>
        Assert.AreEqual(1.12f, HiddenHostsMath.ScoutMultiplier(50, false), Eps);

    [TestMethod]
    public void ScoutMultiplier_SameRealmWithSkill_StaysUnderTheCapBelowIt() =>
        Assert.AreEqual(2.24f, HiddenHostsMath.ScoutMultiplier(50, true), Eps);

    [TestMethod]
    public void ScoutMultiplier_IsCappedAt2Point25() =>
        Assert.AreEqual(2.25f, HiddenHostsMath.ScoutMultiplier(60, true), Eps);

    [TestMethod]
    public void ScoutMultiplier_ExtremeSkill_IsCapped() =>
        Assert.AreEqual(2.25f, HiddenHostsMath.ScoutMultiplier(int.MaxValue, true), Eps);

    [TestMethod]
    public void ScoutMultiplier_HighSkillNotSameRealm_IsCappedToo() =>
        Assert.AreEqual(2.25f, HiddenHostsMath.ScoutMultiplier(1000, false), Eps);

    [TestMethod]
    public void KnownDigits_OneDigit_KnownJustInsideItsThreshold() =>
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(7, 0.279f, 1f, 0));

    [TestMethod]
    public void KnownDigits_OneDigit_UnknownAtAndBeyondItsThreshold()
    {
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(7, 0.28f, 1f, 0));
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(7, 0.281f, 1f, 0));
    }

    [DataTestMethod]
    [DataRow(12, 0.499f, 1)]
    [DataRow(12, 0.501f, 0)]
    [DataRow(12, 0.259f, 2)]
    [DataRow(12, 0.261f, 1)]
    [DataRow(123, 0.619f, 1)]
    [DataRow(123, 0.621f, 0)]
    [DataRow(123, 0.319f, 2)]
    [DataRow(123, 0.321f, 1)]
    [DataRow(123, 0.199f, 3)]
    [DataRow(123, 0.201f, 2)]
    [DataRow(1234, 0.699f, 1)]
    [DataRow(1234, 0.701f, 0)]
    [DataRow(1234, 0.449f, 2)]
    [DataRow(1234, 0.451f, 1)]
    [DataRow(1234, 0.279f, 3)]
    [DataRow(1234, 0.281f, 2)]
    [DataRow(1234, 0.159f, 4)]
    [DataRow(1234, 0.161f, 3)]
    [DataRow(12345, 0.749f, 1)]
    [DataRow(12345, 0.751f, 0)]
    [DataRow(12345, 0.499f, 2)]
    [DataRow(12345, 0.501f, 1)]
    [DataRow(12345, 0.319f, 3)]
    [DataRow(12345, 0.321f, 2)]
    [DataRow(12345, 0.199f, 4)]
    [DataRow(12345, 0.201f, 3)]
    [DataRow(12345, 0.139f, 5)]
    [DataRow(12345, 0.141f, 4)]
    public void KnownDigits_EachRowBoundary(int count, float ratio, int expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.KnownDigits(count, ratio, 1f, 0));

    [TestMethod]
    public void KnownDigits_Count0_IsOneDigit()
    {
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(0, 0.1f, 1f, 0));
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(0, 0.3f, 1f, 0));
    }

    [TestMethod]
    public void KnownDigits_SixDigits_UsesTheFiveRowAndTheLastValueForTheSixth()
    {
        Assert.AreEqual(6, HiddenHostsMath.KnownDigits(123456, 0.139f, 1f, 0));
        Assert.AreEqual(4, HiddenHostsMath.KnownDigits(123456, 0.141f, 1f, 0));
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(123456, 0.751f, 1f, 0));
    }

    [TestMethod]
    public void KnownDigits_Multiplier_WidensEveryThreshold()
    {
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(7, 0.4f, 1f, 0));
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(7, 0.4f, 2f, 0));
    }

    [TestMethod]
    public void KnownDigits_NegativeRatio_IsEverythingKnown() =>
        Assert.AreEqual(3, HiddenHostsMath.KnownDigits(123, -0.5f, 1f, 0));

    [TestMethod]
    public void KnownDigits_Hysteresis_KeepsAPreviouslyKnownDigitInside1Point1x()
    {
        // One digit: threshold 0.28, kept up to 0.308.
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(7, 0.29f, 1f, 0));
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(7, 0.29f, 1f, 1));
    }

    [TestMethod]
    public void KnownDigits_Hysteresis_DropsTheDigitOutside1Point1x() =>
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(7, 0.31f, 1f, 1));

    [TestMethod]
    public void KnownDigits_Hysteresis_AppliesOnlyToDigitsBelowThePreviouslyKnownCount()
    {
        // Three digits at 0.21: digit 2 (threshold 0.20, kept to 0.22) holds only if it was known before.
        Assert.AreEqual(2, HiddenHostsMath.KnownDigits(123, 0.21f, 1f, 2));
        Assert.AreEqual(3, HiddenHostsMath.KnownDigits(123, 0.21f, 1f, 3));
    }

    [TestMethod]
    public void KnownDigits_PreviouslyKnownBeyondTheDigitCount_IsClamped() =>
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(7, 0.29f, 1f, 9));

    [TestMethod]
    public void KnownDigits_KnownDigitsAreAlwaysAPrefix()
    {
        // Two digits at 0.30: digit 0 known (0.50), digit 1 not (0.26).
        Assert.AreEqual(1, HiddenHostsMath.KnownDigits(12, 0.30f, 1f, 0));
        // A previous known of 2 cannot reach past a dropped first digit.
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(12, 0.60f, 1f, 2));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void KnownDigits_NonFiniteRatio_KnowsNothing(float ratio) =>
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(123, ratio, 1f, 3));

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void KnownDigits_UnusableMultiplier_KnowsNothing(float multiplier) =>
        Assert.AreEqual(0, HiddenHostsMath.KnownDigits(123, 0.01f, multiplier, 3));

    [TestMethod]
    public void IsHorizon_BeyondNinetyPercentOfTheSpottingRatio_IsTrue() =>
        Assert.IsTrue(HiddenHostsMath.IsHorizon(0.91f, 1f));

    [TestMethod]
    public void IsHorizon_AtOrInsideNinetyPercent_IsFalse()
    {
        Assert.IsFalse(HiddenHostsMath.IsHorizon(0.9f, 1f));
        Assert.IsFalse(HiddenHostsMath.IsHorizon(0.5f, 1f));
    }

    [TestMethod]
    public void IsHorizon_ScalesWithTheSpottingRatio()
    {
        Assert.IsTrue(HiddenHostsMath.IsHorizon(0.46f, 0.5f));
        Assert.IsFalse(HiddenHostsMath.IsHorizon(0.44f, 0.5f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(0.95f, float.NaN)]
    [DataRow(0.95f, float.PositiveInfinity)]
    [DataRow(0.95f, 0f)]
    [DataRow(0.95f, -1f)]
    public void IsHorizon_NonFiniteOrNonPositiveInput_IsNeverHorizon(float distanceRatio, float spottingRatio) =>
        Assert.IsFalse(HiddenHostsMath.IsHorizon(distanceRatio, spottingRatio));

    [DataTestMethod]
    [DataRow(123, 0, "???")]
    [DataRow(123, 1, "1??")]
    [DataRow(123, 2, "12?")]
    [DataRow(123, 3, "123")]
    [DataRow(123, 4, "123")]
    [DataRow(7, 0, "?")]
    [DataRow(7, 1, "7")]
    [DataRow(0, 0, "?")]
    [DataRow(0, 1, "0")]
    [DataRow(123456, 2, "12????")]
    [DataRow(123, -2, "???")]
    [DataRow(-4, 0, "?")]
    public void FormatMask_KnownPrefixThenQuestionMarks(int count, int known, string expected) =>
        Assert.AreEqual(expected, HiddenHostsMath.FormatMask(count, known));

    [TestMethod]
    public void HorizonText_IsThreeDots() => Assert.AreEqual("...", HiddenHostsMath.HorizonText);
}
