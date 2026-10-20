using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Tests.Features.FellFoes;

/// <summary>
/// Every Fell Foes number is validated before anything reads it: NaN and infinity first, then range and
/// sign, then the ordering between related numbers. A bad value reverts to its default and says so.
/// </summary>
[TestClass]
public class FellFoesTunablesTests
{
    private static readonly float[] NotFinite = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };

    [TestMethod]
    public void Defaults_AreTheShippedNumbers()
    {
        var t = FellFoesTunables.Create();

        Assert.AreEqual(3, t.CorneredMaxFoes);
        Assert.AreEqual(5f, t.CorneredRadius);
        Assert.AreEqual(8, t.ArcherMaxHolding);
        Assert.AreEqual(3, t.ArcherNewPerPass);
        Assert.AreEqual(3, t.StragglerMaxCount);
        Assert.AreEqual(30f, t.StragglerHoldSeconds);
        Assert.AreEqual(4, t.CorneredRallyCap);
        Assert.AreEqual(0, t.Warnings.Count);
    }

    [TestMethod]
    public void Defaults_SatisfyEveryInvariant()
    {
        var t = FellFoesTunables.Create();

        Assert.IsTrue(t.CorneredRadius <= FellFoesTunables.MaxNearbyRadius, "radii stay under the 15 m cap, a cheap bound on the foe-count query");
        Assert.IsTrue(t.ArcherSiegeRange <= t.ArcherFieldRange);
        Assert.IsTrue(t.StragglerMaxCount <= t.StragglerPeakMin);
        Assert.IsTrue(t.ArcherChance >= 0f && t.ArcherChance <= 1f);
    }

    [TestMethod]
    public void FloatFields_NotFinite_RevertToDefaultWithAWarning()
    {
        foreach (var bad in NotFinite)
        {
            var t = FellFoesTunables.Create(
                corneredRadius: bad, corneredMinHealthFraction: bad, archerChance: bad, archerHoldSeconds: bad,
                archerMinMorale: bad, archerFieldRange: bad, archerSiegeRange: bad, stragglerHoldSeconds: bad);

            Assert.AreEqual(5f, t.CorneredRadius, bad.ToString());
            Assert.AreEqual(FellFoesTunables.DefaultCorneredMinHealthFraction, t.CorneredMinHealthFraction);
            Assert.AreEqual(FellFoesTunables.DefaultArcherChance, t.ArcherChance);
            Assert.AreEqual(FellFoesTunables.DefaultArcherHoldSeconds, t.ArcherHoldSeconds);
            Assert.AreEqual(FellFoesTunables.DefaultArcherMinMorale, t.ArcherMinMorale);
            Assert.AreEqual(FellFoesTunables.DefaultArcherFieldRange, t.ArcherFieldRange);
            Assert.AreEqual(FellFoesTunables.DefaultArcherSiegeRange, t.ArcherSiegeRange);
            Assert.AreEqual(30f, t.StragglerHoldSeconds);
            Assert.AreEqual(8, t.Warnings.Count, "one warning per reverted field");
        }
    }

    [DataTestMethod]
    [DataRow(0.5f)]
    [DataRow(-5f)]
    [DataRow(15.01f)]
    [DataRow(100f)]
    public void CorneredRadius_OutOfRange_RevertsToDefault(float radius)
    {
        var t = FellFoesTunables.Create(corneredRadius: radius);

        Assert.AreEqual(FellFoesTunables.DefaultCorneredRadius, t.CorneredRadius);
        Assert.AreEqual(1, t.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow(1f)]
    [DataRow(15f)]
    public void CorneredRadius_AtTheBounds_IsKept(float radius)
    {
        Assert.AreEqual(radius, FellFoesTunables.Create(corneredRadius: radius).CorneredRadius);
    }

    [DataTestMethod]
    [DataRow(-0.1f)]
    [DataRow(1.01f)]
    public void ArcherChance_OutsideZeroToOne_RevertsToDefault(float chance)
    {
        Assert.AreEqual(FellFoesTunables.DefaultArcherChance, FellFoesTunables.Create(archerChance: chance).ArcherChance);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(1f)]
    [DataRow(0.4f)]
    public void ArcherChance_InsideZeroToOne_IsKept(float chance)
    {
        Assert.AreEqual(chance, FellFoesTunables.Create(archerChance: chance).ArcherChance);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(11)]
    public void CorneredMaxFoes_OutOfRange_RevertsToDefault(int foes)
    {
        Assert.AreEqual(3, FellFoesTunables.Create(corneredMaxFoes: foes).CorneredMaxFoes);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    [DataRow(31)]
    public void ArcherMaxHolding_OutOfRange_RevertsToDefault(int cap)
    {
        Assert.AreEqual(8, FellFoesTunables.Create(archerMaxHolding: cap).ArcherMaxHolding);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(11)]
    public void ArcherNewPerPass_OutOfRange_RevertsToDefault(int perPass)
    {
        Assert.AreEqual(3, FellFoesTunables.Create(archerNewPerPass: perPass).ArcherNewPerPass);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-2)]
    [DataRow(21)]
    public void StragglerMaxCount_OutOfRange_RevertsToDefault(int count)
    {
        Assert.AreEqual(3, FellFoesTunables.Create(stragglerMaxCount: count).StragglerMaxCount);
    }

    [DataTestMethod]
    [DataRow(4f)]
    [DataRow(-30f)]
    [DataRow(301f)]
    public void StragglerHoldSeconds_OutOfRange_RevertsToDefault(float seconds)
    {
        Assert.AreEqual(30f, FellFoesTunables.Create(stragglerHoldSeconds: seconds).StragglerHoldSeconds);
    }

    [TestMethod]
    public void ArcherSiegeRange_AboveTheFieldRange_BothRevertToDefaults()
    {
        var t = FellFoesTunables.Create(archerFieldRange: 30f, archerSiegeRange: 60f);

        Assert.AreEqual(FellFoesTunables.DefaultArcherFieldRange, t.ArcherFieldRange);
        Assert.AreEqual(FellFoesTunables.DefaultArcherSiegeRange, t.ArcherSiegeRange);
        Assert.IsTrue(t.Warnings.Any(w => w.Contains("siege")));
    }

    [TestMethod]
    public void ArcherSiegeRange_EqualToTheFieldRange_IsKept()
    {
        var t = FellFoesTunables.Create(archerFieldRange: 30f, archerSiegeRange: 30f);

        Assert.AreEqual(30f, t.ArcherSiegeRange);
        Assert.AreEqual(0, t.Warnings.Count);
    }

    [TestMethod]
    public void StragglerMaxCount_AbovePeakMin_BothRevertToDefaults()
    {
        var t = FellFoesTunables.Create(stragglerMaxCount: 20, stragglerPeakMin: 10);

        Assert.AreEqual(FellFoesTunables.DefaultStragglerMaxCount, t.StragglerMaxCount);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerPeakMin, t.StragglerPeakMin);
        Assert.IsTrue(t.Warnings.Any(w => w.Contains("peak")));
    }

    [TestMethod]
    public void StragglerPeakMin_OutOfRange_RevertsToDefault()
    {
        Assert.AreEqual(FellFoesTunables.DefaultStragglerPeakMin, FellFoesTunables.Create(stragglerPeakMin: 1).StragglerPeakMin);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerPeakMin, FellFoesTunables.Create(stragglerPeakMin: 5001).StragglerPeakMin);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(11)]
    public void CorneredRallyCap_OutOfRange_RevertsToDefault(int cap)
    {
        Assert.AreEqual(4, FellFoesTunables.Create(corneredRallyCap: cap).CorneredRallyCap);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(10)]
    public void CorneredRallyCap_AtTheBounds_IsKept(int cap)
    {
        Assert.AreEqual(cap, FellFoesTunables.Create(corneredRallyCap: cap).CorneredRallyCap);
    }

    [DataTestMethod]
    [DataRow(1f, 2f)]
    [DataRow(5f, 10f)]
    [DataRow(7.5f, 15f)]
    [DataRow(10f, 15f)]
    [DataRow(15f, 15f)]
    public void HoldRadius_IsTwiceTheCorneredRadius_ClampedToTheNearbyCap(float radius, float hold)
    {
        Assert.AreEqual(hold, FellFoesTunables.Create(corneredRadius: radius).HoldRadius);
    }

    [TestMethod]
    public void ArcherMinMorale_OutsideZeroToHundred_RevertsToDefault()
    {
        Assert.AreEqual(FellFoesTunables.DefaultArcherMinMorale, FellFoesTunables.Create(archerMinMorale: -1f).ArcherMinMorale);
        Assert.AreEqual(FellFoesTunables.DefaultArcherMinMorale, FellFoesTunables.Create(archerMinMorale: 100.5f).ArcherMinMorale);
    }
}
