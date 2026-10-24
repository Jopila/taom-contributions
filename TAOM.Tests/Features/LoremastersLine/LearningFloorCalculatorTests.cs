using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LoremastersLine;

namespace TAOM.Tests.Features.LoremastersLine;

[TestClass]
public class LearningFloorCalculatorTests
{
    // The vanilla shape (DefaultCharacterDevelopmentModel): rate = 1.25 * (1 + 0.4a + f - (1 + 0.1n)) once n = skill - limit
    // is positive, floored at 0, so the rate reaches 0 at limit + 4a + 10f.
    private static Func<int, float> Vanilla(int limit, float attribute, int focus) => skill =>
    {
        var factor = 1f + 0.4f * attribute + focus;
        if (skill > limit) factor += -1f - 0.1f * (skill - limit);
        return Math.Max(0f, 1.25f * factor);
    };

    private static Func<int, float> Counting(Func<int, float> inner, Action onCall) => skill =>
    {
        onCall();
        return inner(skill);
    };

    [TestMethod]
    public void VanillaShaped_FloorIsAtLimitPlusFourAttributePlusTenFocus()
    {
        var floor = LearningFloorCalculator.Find(Vanilla(limit: 40, attribute: 5f, focus: 2));

        Assert.IsTrue(floor.HasFloor);
        Assert.AreEqual(40 + 4 * 5 + 10 * 2, floor.Level);
        Assert.AreEqual(0f, floor.FloorRate, 1e-4f);
        Assert.AreEqual(1.25f * (1f + 2f + 2f), floor.FullRate, 1e-4f);
    }

    [TestMethod]
    public void Steep_FloorIsFoundOneLevelPastTheLimit()
    {
        // A model that drops to 0 the level after skill 7.
        var floor = LearningFloorCalculator.Find(skill => skill <= 7 ? 3f : 0f);

        Assert.IsTrue(floor.HasFloor);
        Assert.AreEqual(8, floor.Level);
    }

    [TestMethod]
    public void Flat_HasNoFloor()
    {
        var floor = LearningFloorCalculator.Find(_ => 1.25f);

        Assert.IsFalse(floor.HasFloor);
    }

    [TestMethod]
    public void FloorAtSkillOne_IsFoundAtOne()
    {
        var floor = LearningFloorCalculator.Find(skill => skill == 0 ? 1f : 0.2f);

        Assert.IsTrue(floor.HasFloor);
        Assert.AreEqual(1, floor.Level);
        Assert.AreEqual(0.2f, floor.FloorRate, 1e-6f);
        Assert.AreEqual(1f, floor.FullRate, 1e-6f);
    }

    [TestMethod]
    public void FloorOnlyAtTheProbeCeiling_IsFoundAt4096()
    {
        var floor = LearningFloorCalculator.Find(skill => 5f - skill * (5f / 4096f));

        Assert.IsTrue(floor.HasFloor);
        Assert.AreEqual(4096, floor.Level);
    }

    [TestMethod]
    public void NaNOrInfinityAtSkillZero_HasNoFloor()
    {
        Assert.IsFalse(LearningFloorCalculator.Find(_ => float.NaN).HasFloor);
        Assert.IsFalse(LearningFloorCalculator.Find(skill => skill == 0 ? float.PositiveInfinity : 0f).HasFloor);
        Assert.IsFalse(LearningFloorCalculator.Find(skill => skill == 0 ? float.NegativeInfinity : 0f).HasFloor);
    }

    [TestMethod]
    public void NaNAtTheCeiling_HasNoFloor()
    {
        Assert.IsFalse(LearningFloorCalculator.Find(skill => skill >= 4096 ? float.NaN : 2f).HasFloor);
    }

    [TestMethod]
    public void NaNMidSearch_HasNoFloor()
    {
        var curve = Vanilla(limit: 40, attribute: 5f, focus: 2);

        // 2048 is the first midpoint the bisection probes.
        var floor = LearningFloorCalculator.Find(skill => skill == 2048 ? float.NaN : curve(skill));

        Assert.IsFalse(floor.HasFloor);
    }

    [TestMethod]
    public void NegativeRates_AreFoundLikeAnyOther()
    {
        var floor = LearningFloorCalculator.Find(skill => skill <= 30 ? 2f : -0.5f);

        Assert.IsTrue(floor.HasFloor);
        Assert.AreEqual(31, floor.Level);
        Assert.AreEqual(-0.5f, floor.FloorRate, 1e-6f);
    }

    [TestMethod]
    public void NonMonotonic_ReturnsABoundary_WhereTheRateIsAtTheFloorAndOneLevelBelowIsNot()
    {
        // A bump in the middle: high, low, high again, then the true floor. Bisection cannot promise the lowest
        // boundary, but the level it returns must be a boundary.
        Func<int, float> rate = skill => skill < 100 ? 4f : skill < 700 ? 1f : skill < 900 ? 3f : 0f;

        var floor = LearningFloorCalculator.Find(rate);

        Assert.IsTrue(floor.HasFloor);
        Assert.IsTrue(Math.Abs(rate(floor.Level) - floor.FloorRate) <= 1e-4f, "the rate at the level is the floor");
        Assert.IsTrue(Math.Abs(rate(floor.Level - 1) - floor.FloorRate) > 1e-4f, "one level below is not");
    }

    [TestMethod]
    public void ProbeCount_IsAtMostFourteen()
    {
        var calls = 0;

        LearningFloorCalculator.Find(Counting(Vanilla(40, 5f, 2), () => calls++));

        Assert.IsTrue(calls <= 14, "probes: " + calls);
    }

    [TestMethod]
    public void FlatCurve_StopsAfterTheTwoEndProbes()
    {
        var calls = 0;

        LearningFloorCalculator.Find(Counting(_ => 1f, () => calls++));

        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void RisingCurve_HasNoFloor_AndStopsAfterTheTwoEndProbes()
    {
        var calls = 0;

        var floor = LearningFloorCalculator.Find(Counting(skill => skill == 0 ? 1f : 2f, () => calls++));

        Assert.IsFalse(floor.HasFloor, "a rate that rises past skill 0 has no slowest point to mark");
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void BarLevels_IsMaxLevelPlusTwentyFive_WithNoClamp()
    {
        Assert.AreEqual(25, LearningFloorCalculator.BarOverhang);
        Assert.AreEqual(325, LearningFloorCalculator.BarLevels(300));
        Assert.AreEqual(0, LearningFloorCalculator.BarLevels(-25));
        Assert.AreEqual(-75, LearningFloorCalculator.BarLevels(-100), "the sum is returned as is; callers gate on above 0");
    }

    [TestMethod]
    public void IsOnBar_ShowsUpToTheLastLevelBeforeTheEnd_AndHidesFromTheEnd()
    {
        Assert.IsTrue(LearningFloorCalculator.IsOnBar(At(300 + 24), 300), "MaxLevel + 24 shows");
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(At(300 + 25), 300), "MaxLevel + 25 hides: a zero-wide band");
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(At(900), 300));
        Assert.IsTrue(LearningFloorCalculator.IsOnBar(At(1), 300));
    }

    [TestMethod]
    public void IsOnBar_HidesWithoutAFloor_AndOnABarOfNoWidth()
    {
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(LearningFloor.None, 300));
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(At(0), -25), "a bar of zero width");
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(At(0), -100), "a bar of negative width");
        Assert.IsFalse(LearningFloorCalculator.IsOnBar(At(1), -25));
    }

    [TestMethod]
    public void BandLevels_IsTheSpanFromTheFloorToTheEndOfTheBar()
    {
        Assert.AreEqual(325 - 100, LearningFloorCalculator.BandLevels(At(100), 300));
        Assert.AreEqual(1, LearningFloorCalculator.BandLevels(At(324), 300));
        Assert.AreEqual(325, LearningFloorCalculator.BandLevels(At(0), 300));
    }

    [TestMethod]
    public void ShownPercent_IsThePercentOfFull_ForASkillThatStillLearns()
    {
        Assert.AreEqual(40, LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 0.5f, 1.25f)));
        Assert.AreEqual(100, LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 1f, 1f)));
    }

    [TestMethod]
    public void ShownPercent_IsNull_WhenTheSkillCannotLearnOrTheFullRateIsUnusable()
    {
        Assert.IsNull(LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 0f, 1.25f)), "capped");
        Assert.IsNull(LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 0.004f, 1.25f)), "rounds to 0.00");
        Assert.IsNull(LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, float.NaN, 1.25f)));
        Assert.IsNull(LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 0.5f, 0f)), "no full rate to share");
        Assert.IsNull(LearningFloorCalculator.ShownPercent(new LearningFloor(true, 60, 0.5f, float.NaN)));
    }

    private static LearningFloor At(int level) => new LearningFloor(true, level, 0f, 1f);

    [TestMethod]
    public void PercentOfFull_RoundsAndClamps()
    {
        Assert.AreEqual(40, LearningFloorCalculator.PercentOfFull(0.5f, 1.25f));
        Assert.AreEqual(100, LearningFloorCalculator.PercentOfFull(1f, 1f));
        Assert.AreEqual(0, LearningFloorCalculator.PercentOfFull(0f, 1.25f));
        Assert.AreEqual(0, LearningFloorCalculator.PercentOfFull(-0.5f, 1.25f), "a negative floor counts as 0");
        Assert.AreEqual(999, LearningFloorCalculator.PercentOfFull(500f, 1f), "the clamp");
    }

    [TestMethod]
    public void PercentOfFull_HasNoAnswerForABadFullRate()
    {
        Assert.IsNull(LearningFloorCalculator.PercentOfFull(0.5f, 0f));
        Assert.IsNull(LearningFloorCalculator.PercentOfFull(0.5f, -1f));
        Assert.IsNull(LearningFloorCalculator.PercentOfFull(0.5f, float.NaN));
        Assert.IsNull(LearningFloorCalculator.PercentOfFull(0.5f, float.PositiveInfinity));
        Assert.IsNull(LearningFloorCalculator.PercentOfFull(float.NaN, 1.25f));
    }

    [TestMethod]
    public void CannotLearn_FollowsVanillasTwoDecimalRule()
    {
        Assert.IsTrue(LearningFloorCalculator.CannotLearn(0f));
        Assert.IsTrue(LearningFloorCalculator.CannotLearn(0.004f), "rounds to 0.00, as SkillVM.CanLearnSkill does");
        Assert.IsTrue(LearningFloorCalculator.CannotLearn(-1f));
        Assert.IsFalse(LearningFloorCalculator.CannotLearn(0.006f));
        Assert.IsFalse(LearningFloorCalculator.CannotLearn(1.25f));
    }

    [TestMethod]
    public void CannotLearn_TreatsNaNAsCannotLearn()
    {
        Assert.IsTrue(LearningFloorCalculator.CannotLearn(float.NaN), "vanilla's CanLearnSkill is a greater-than test, false for NaN");
    }
}
