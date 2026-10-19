using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WatchfulHolds;

namespace TAOM.Tests.Features.WatchfulHolds;

/// <summary>The arithmetic the engine adapter relies on, kept out of the adapter so it runs without the game.</summary>
[TestClass]
public class WatchfulLossMathTests
{
    // ---- CountFromFraction --------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(200, 0.01f, 1f, 2)]
    [DataRow(300, 0.01f, 1f, 3)]
    [DataRow(300, 0.02f, 1f, 6)]
    [DataRow(99, 0.02f, 1f, 1)]
    [DataRow(99, 0.01f, 1f, 0)]
    [DataRow(200, 0.01f, 2f, 4)]
    [DataRow(200, 0.01f, 0f, 0)]
    [DataRow(0, 0.5f, 1f, 0)]
    [DataRow(-5, 0.5f, 1f, 0)]
    [DataRow(1000, 1f, 5f, 5000)]
    public void CountFromFraction_FloorsTheProduct_ForgivingSinglePrecisionDust(int men, float fraction, float multiplier, int expected)
    {
        Assert.AreEqual(expected, WatchfulLossMath.CountFromFraction(men, fraction, multiplier));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(0.5f, float.NaN)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(0.5f, float.PositiveInfinity)]
    [DataRow(-0.5f, 1f)]
    [DataRow(0.5f, -1f)]
    public void CountFromFraction_NonFiniteOrNegativeInput_IsZero(float fraction, float multiplier)
    {
        Assert.AreEqual(0, WatchfulLossMath.CountFromFraction(1000, fraction, multiplier));
    }

    // ---- ClampToHealthy -----------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(4, 10, 4)]
    [DataRow(10, 10, 10)]
    [DataRow(11, 10, 10)]
    [DataRow(-3, 10, 0)]
    [DataRow(5, 0, 0)]
    [DataRow(5, -2, 0)]
    public void ClampToHealthy_NeverExceedsTheHealthyRegulars_NorGoesNegative(int requested, int healthy, int expected)
    {
        Assert.AreEqual(expected, WatchfulLossMath.ClampToHealthy(requested, healthy));
    }

    [TestMethod]
    public void ClampToHealthy_KillThenWound_WoundSeesTheHealthyCountAfterTheKills()
    {
        // 10 healthy regulars, 8 to kill, 8 to wound: the wound step must see 2 left, not 10.
        int healthy = 10;
        int kill = WatchfulLossMath.ClampToHealthy(8, healthy);
        healthy -= kill;
        int wound = WatchfulLossMath.ClampToHealthy(8, healthy);

        Assert.AreEqual(8, kill);
        Assert.AreEqual(2, wound);
    }

    // ---- CohesionAfterHit ---------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(50f, 5f, 10f, 45f)]
    [DataRow(12f, 5f, 10f, 10f)]
    [DataRow(15f, 5f, 10f, 10f)]
    [DataRow(10f, 5f, 10f, 10f)]
    [DataRow(4f, 5f, 10f, 4f)]
    [DataRow(0f, 5f, 10f, 0f)]
    [DataRow(50f, 0f, 10f, 50f)]
    [DataRow(50f, -3f, 10f, 50f)]
    [DataRow(50f, 100f, 0f, 0f)]
    public void CohesionAfterHit_FallsByTheLoss_NeverBelowTheFloor_NeverRaised(float current, float loss, float floor, float expected)
    {
        Assert.AreEqual(expected, WatchfulLossMath.CohesionAfterHit(current, loss, floor));
    }

    [TestMethod]
    public void CohesionAfterHit_NonFiniteInput_LeavesTheCohesionAlone()
    {
        Assert.AreEqual(50f, WatchfulLossMath.CohesionAfterHit(50f, float.NaN, 10f));
        Assert.AreEqual(50f, WatchfulLossMath.CohesionAfterHit(50f, float.PositiveInfinity, 10f));
        Assert.AreEqual(50f, WatchfulLossMath.CohesionAfterHit(50f, 5f, float.NaN));
        Assert.IsTrue(float.IsNaN(WatchfulLossMath.CohesionAfterHit(float.NaN, 5f, 10f)));
    }

    // ---- FoodUnitsToRemove --------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(100, 0.05f, 5)]
    [DataRow(101, 0.05f, 6)]
    [DataRow(1, 0.05f, 1)]
    [DataRow(0, 0.05f, 0)]
    [DataRow(-4, 0.05f, 0)]
    [DataRow(100, 0f, 0)]
    [DataRow(100, 1f, 100)]
    [DataRow(100, 2f, 100)]
    [DataRow(2000000000, 1f, 2000000000)]
    public void FoodUnitsToRemove_IsTheCeilingOfTheShare_CappedAtTheTotal(int total, float fraction, int expected)
    {
        Assert.AreEqual(expected, WatchfulLossMath.FoodUnitsToRemove(total, fraction));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.5f)]
    public void FoodUnitsToRemove_BadFraction_RemovesNothing(float fraction)
    {
        Assert.AreEqual(0, WatchfulLossMath.FoodUnitsToRemove(100, fraction));
    }

    // ---- PlanFoodLoss -------------------------------------------------------------------------

    [TestMethod]
    public void PlanFoodLoss_RemovesTheWantedUnits_NeverMoreThanAStackHolds()
    {
        var stacks = new[] { 10, 20, 30 };

        for (int seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var plan = WatchfulLossMath.PlanFoodLoss(stacks, 0.5f, n => rng.Next(n));

            Assert.AreEqual(30, plan.Sum(), "ceil(60 x 0.5) units, seed " + seed);
            for (int i = 0; i < stacks.Length; i++)
                Assert.IsTrue(plan[i] >= 0 && plan[i] <= stacks[i], $"stack {i}, seed {seed}");
        }
    }

    [TestMethod]
    public void PlanFoodLoss_CanTakeEverything()
    {
        var plan = WatchfulLossMath.PlanFoodLoss(new[] { 3, 4 }, 1f, n => n - 1);

        CollectionAssert.AreEqual(new[] { 3, 4 }, plan);
    }

    [TestMethod]
    public void PlanFoodLoss_ExtremeRandomValues_StillTerminateAndBalance()
    {
        var stacks = new[] { 5, 0, 7, 1 };

        foreach (Func<int, int> pick in new Func<int, int>[] { n => 0, n => n - 1, n => n + 50, n => -3 })
        {
            var plan = WatchfulLossMath.PlanFoodLoss(stacks, 0.6f, pick);

            Assert.AreEqual(8, plan.Sum(), "ceil(13 x 0.6)");
            for (int i = 0; i < stacks.Length; i++)
                Assert.IsTrue(plan[i] >= 0 && plan[i] <= stacks[i]);
        }
    }

    [TestMethod]
    public void PlanFoodLoss_NoFood_IsAnEmptyPlan()
    {
        CollectionAssert.AreEqual(new int[0], WatchfulLossMath.PlanFoodLoss(new int[0], 0.5f, n => 0));
        CollectionAssert.AreEqual(new[] { 0, 0 }, WatchfulLossMath.PlanFoodLoss(new[] { 0, 0 }, 0.5f, n => 0));
    }

    [TestMethod]
    public void PlanFoodLoss_ZeroFraction_RemovesNothing()
    {
        CollectionAssert.AreEqual(new[] { 0, 0 }, WatchfulLossMath.PlanFoodLoss(new[] { 5, 5 }, 0f, n => 0));
    }

    [TestMethod]
    public void PlanFoodLoss_IgnoresNegativeStacks()
    {
        var plan = WatchfulLossMath.PlanFoodLoss(new[] { -5, 10 }, 0.5f, n => 0);

        CollectionAssert.AreEqual(new[] { 0, 5 }, plan);
    }
}
