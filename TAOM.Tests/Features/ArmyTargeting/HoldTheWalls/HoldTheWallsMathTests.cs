using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.HoldTheWalls;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>#871: the pure rules of Hold the Walls, with the trait extremes, the clamps and the 32 hour cap.</summary>
[TestClass]
public class HoldTheWallsMathTests
{
    private static readonly CapturedFortHoldConfig Defaults = new CapturedFortHoldConfig();

    [DataTestMethod]
    [DataRow(0, 0, 22f)]
    [DataRow(0, 1, 27f)]
    [DataRow(1, 0, 17f)]
    [DataRow(1, 1, 22f)]
    [DataRow(-1, 0, 27f)]
    [DataRow(0, 2, 30f)]
    [DataRow(2, 0, 12f)]
    public void HoldHours_FollowsCalculatingMinusValor(int valor, int calculating, float expected)
    {
        Assert.AreEqual(expected, HoldTheWallsMath.HoldHours(valor, calculating, Defaults));
    }

    [TestMethod]
    public void HoldHours_MostCalculatingLeastValorous_ClampsToTheLongestHold()
    {
        Assert.AreEqual(30f, HoldTheWallsMath.HoldHours(-2, 2, Defaults), "22 + 5 x 4 = 42 clamps to 30");
    }

    [TestMethod]
    public void HoldHours_MostValorousLeastCalculating_ClampsToTheShortestHold()
    {
        Assert.AreEqual(12f, HoldTheWallsMath.HoldHours(2, -2, Defaults), "22 - 5 x 4 = 2 clamps to 12");
    }

    [TestMethod]
    public void HoldHours_AHandBuiltLongestHoldAboveTheCap_StaysAt32()
    {
        var config = new CapturedFortHoldConfig { MaxHours = 90f, BaseHours = 90f };

        Assert.AreEqual(32f, HoldTheWallsMath.HoldHours(0, 0, config), "the hold counts toward the 48 hour inactivity disband, which release does not reset");
    }

    [TestMethod]
    public void HoldHours_AHandBuiltShortestHoldAtOrBelowVanilla_StaysAbove10()
    {
        var config = new CapturedFortHoldConfig { MinHours = 2f, BaseHours = 1f };

        Assert.AreEqual(11f, HoldTheWallsMath.HoldHours(0, 0, config), "a hold never shortens the engine's own 10 hour pause");
    }

    [TestMethod]
    public void HoldHours_ExtremeTraitLevels_DoNotOverflow()
    {
        Assert.AreEqual(30f, HoldTheWallsMath.HoldHours(int.MinValue, int.MaxValue, Defaults));
        Assert.AreEqual(12f, HoldTheWallsMath.HoldHours(int.MaxValue, int.MinValue, Defaults));
    }

    [TestMethod]
    public void HoldHours_NaNConfig_FallsToTheShortestHold()
    {
        var config = new CapturedFortHoldConfig { BaseHours = float.NaN };

        Assert.AreEqual(12f, HoldTheWallsMath.HoldHours(0, 0, config));
    }

    [DataTestMethod]
    [DataRow(0.0, 0)]
    [DataRow(-3.0, 0)]
    [DataRow(0.0001, 1)]
    [DataRow(1.0, 1)]
    [DataRow(5.2, 6)]
    [DataRow(29.0, 29)]
    [DataRow(32.0, 32)]
    [DataRow(33.0, 32)]
    [DataRow(1000.0, 32)]
    public void EngineHours_RoundsUpAndCapsAt32(double remaining, int expected)
    {
        Assert.AreEqual(expected, HoldTheWallsMath.EngineHours(remaining));
    }

    [TestMethod]
    public void EngineHours_NonFinite_IsZero()
    {
        Assert.AreEqual(0, HoldTheWallsMath.EngineHours(double.NaN));
        Assert.AreEqual(0, HoldTheWallsMath.EngineHours(double.PositiveInfinity));
        Assert.AreEqual(0, HoldTheWallsMath.EngineHours(double.NegativeInfinity));
    }

    [TestMethod]
    public void VanillaRemainingHours_IsTenHoursFromTheCapture()
    {
        Assert.AreEqual(7.0, HoldTheWallsMath.VanillaRemainingHours(100.0, 103.0), 1e-9);
        Assert.AreEqual(-5.0, HoldTheWallsMath.VanillaRemainingHours(100.0, 115.0), 1e-9);
    }

    [TestMethod]
    public void ReleaseReason_AFineArmy_HasNone()
    {
        Assert.AreEqual(HoldEnd.None, HoldTheWallsMath.ReleaseReason(80f, 5f, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_UnderSiege_WinsOverTheOthers()
    {
        Assert.AreEqual(HoldEnd.Siege, HoldTheWallsMath.ReleaseReason(10f, 0f, true, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_CohesionBelowTheFloor_IsCohesion_AndAtTheFloorIsNot()
    {
        Assert.AreEqual(HoldEnd.Cohesion, HoldTheWallsMath.ReleaseReason(39.9f, 5f, false, Defaults));
        Assert.AreEqual(HoldEnd.None, HoldTheWallsMath.ReleaseReason(40f, 5f, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_FoodBelowTheFloor_IsFood_AndAtTheFloorIsNot()
    {
        Assert.AreEqual(HoldEnd.Food, HoldTheWallsMath.ReleaseReason(80f, 0.9f, false, Defaults));
        Assert.AreEqual(HoldEnd.None, HoldTheWallsMath.ReleaseReason(80f, 1f, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_CohesionIsCheckedBeforeFood()
    {
        Assert.AreEqual(HoldEnd.Cohesion, HoldTheWallsMath.ReleaseReason(10f, 0f, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_NaNCohesion_FailsTheGate()
    {
        Assert.AreEqual(HoldEnd.Cohesion, HoldTheWallsMath.ReleaseReason(float.NaN, 5f, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_NaNFood_FailsTheGate()
    {
        Assert.AreEqual(HoldEnd.Food, HoldTheWallsMath.ReleaseReason(80f, float.NaN, false, Defaults));
    }

    [TestMethod]
    public void ReleaseReason_InfiniteFood_PassesTheGate()
    {
        Assert.AreEqual(HoldEnd.None, HoldTheWallsMath.ReleaseReason(80f, float.PositiveInfinity, false, Defaults));
    }

    [TestMethod]
    public void MayStart_NeedsCohesionAtTheHoldFloor_Food_AndNoSiege()
    {
        Assert.IsTrue(HoldTheWallsMath.MayStart(45f, 3f, false, Defaults));
        Assert.IsFalse(HoldTheWallsMath.MayStart(44.9f, 3f, false, Defaults));
        Assert.IsFalse(HoldTheWallsMath.MayStart(80f, 0.5f, false, Defaults), "it would be released at once");
        Assert.IsFalse(HoldTheWallsMath.MayStart(80f, 3f, true, Defaults), "a fortress under siege would release it at once");
        Assert.IsFalse(HoldTheWallsMath.MayStart(float.NaN, 3f, false, Defaults));
        Assert.IsFalse(HoldTheWallsMath.MayStart(80f, float.NaN, false, Defaults));
    }

    [TestMethod]
    public void MayStart_AgreesWithReleaseReason_ForAHoldFloorBelowTheReleaseFloorsArmy()
    {
        var config = new CapturedFortHoldConfig { MinCohesionToHold = 50f, ReleaseCohesion = 50f };

        Assert.IsTrue(HoldTheWallsMath.MayStart(50f, 3f, false, config));
        Assert.AreEqual(HoldEnd.None, HoldTheWallsMath.ReleaseReason(50f, 3f, false, config));
    }
}
