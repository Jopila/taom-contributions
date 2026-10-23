using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BillsBurden;

namespace TAOM.Tests.Features.BillsBurden;

[TestClass]
public class CargoFillCalculatorTests
{
    [TestMethod]
    public void Compute_HalfFull_Is50NoWarning()
    {
        var fill = CargoFillCalculator.Compute(50f, 100);

        Assert.IsTrue(fill.IsValid);
        Assert.AreEqual(50, fill.Percent);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_ExactlyFull_Is100NoWarning()
    {
        var fill = CargoFillCalculator.Compute(100f, 100);

        Assert.AreEqual(100, fill.Percent);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_SlightlyOver_ShowsRoundedDownPercentButWarns()
    {
        var fill = CargoFillCalculator.Compute(100.4f, 100);

        Assert.AreEqual(100, fill.Percent);
        Assert.IsTrue(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_Overloaded_Is150Warning()
    {
        var fill = CargoFillCalculator.Compute(150f, 100);

        Assert.AreEqual(150, fill.Percent);
        Assert.IsTrue(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_Midpoint_RoundsAwayFromZero()
    {
        Assert.AreEqual(13, CargoFillCalculator.Compute(1f, 8).Percent);
    }

    // The mixin copies Percent and HasWarning to the row without checking IsValid, so an invalid
    // fill must carry 0 and no warning: a "-" row never turns red.
    [TestMethod]
    public void Compute_ZeroCapacity_IsInvalidWithZeroPercentAndNoWarning()
    {
        var fill = CargoFillCalculator.Compute(10f, 0);

        Assert.IsFalse(fill.IsValid);
        Assert.AreEqual(0, fill.Percent);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_NegativeCapacity_IsInvalid()
    {
        Assert.IsFalse(CargoFillCalculator.Compute(10f, -5).IsValid);
    }

    [TestMethod]
    public void Compute_NaNWeight_IsInvalidWithNoWarning()
    {
        var fill = CargoFillCalculator.Compute(float.NaN, 100);

        Assert.IsFalse(fill.IsValid);
        Assert.AreEqual(0, fill.Percent);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_InfiniteWeight_IsInvalidWithNoWarning()
    {
        var fill = CargoFillCalculator.Compute(float.PositiveInfinity, 100);

        Assert.IsFalse(fill.IsValid);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_NegativeWeight_ClampsToZero()
    {
        var fill = CargoFillCalculator.Compute(-20f, 100);

        Assert.IsTrue(fill.IsValid);
        Assert.AreEqual(0, fill.Percent);
        Assert.IsFalse(fill.HasWarning);
    }

    [TestMethod]
    public void Compute_HugeRatio_ClampsTo999()
    {
        var fill = CargoFillCalculator.Compute(1e30f, 1);

        Assert.IsTrue(fill.IsValid);
        Assert.AreEqual(999, fill.Percent);
        Assert.IsTrue(fill.HasWarning);
    }
}
