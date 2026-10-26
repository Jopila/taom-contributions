using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.FieldCamp.Activities;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The arithmetic and the ground tables of camp work (#860, docs/features/field-camp.md "Camp work"): the yield, which terrain
/// each work can use, where fishing is allowed, the single injury roll, the morale and the tools. Pure, so no game is needed.
/// </summary>
[TestClass]
public class CampActivityRulesTests
{
    // ---- Yield

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(5, 5)]
    [DataRow(10, 10)]
    [DataRow(25, 25)]
    [DataRow(1000, 30)]
    public void Yield_TenPerTenMen_CappedAtThirty(int men, int expected) =>
        Assert.AreEqual(expected, CampActivityRules.Yield(10f, 30, men, 1f));

    [TestMethod]
    public void Yield_ScalesWithTheGroundFactor_AndRounds()
    {
        Assert.AreEqual(7, CampActivityRules.Yield(10f, 30, 10, 0.7f));
        Assert.AreEqual(5, CampActivityRules.Yield(10f, 30, 10, 0.5f));
        Assert.AreEqual(2, CampActivityRules.Yield(10f, 30, 10, 0.2f));
        Assert.AreEqual(0, CampActivityRules.Yield(10f, 30, 10, 0f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f, 30)]
    [DataRow(float.PositiveInfinity, 1f, 30)]
    [DataRow(-10f, 1f, 30)]
    [DataRow(10f, float.NaN, 30)]
    [DataRow(10f, -1f, 30)]
    [DataRow(10f, 1f, -5)]
    [DataRow(10f, 1f, 0)]
    public void Yield_BadInput_PaysNothing(float perTen, float factor, int cap) =>
        Assert.AreEqual(0, CampActivityRules.Yield(perTen, cap, 20, factor));

    [TestMethod]
    public void Yield_NegativeMen_PaysNothing() => Assert.AreEqual(0, CampActivityRules.Yield(10f, 30, -4, 1f));

    [TestMethod]
    public void Yield_AHugeProduct_StaysAtTheCap_NotAWrappedCast() =>
        Assert.AreEqual(200, CampActivityRules.Yield(float.MaxValue, 200, int.MaxValue, 1f));

    // ---- Ground

    [DataTestMethod]
    [DataRow(TerrainType.Forest, 1f)]
    [DataRow(TerrainType.Plain, 0.7f)]
    [DataRow(TerrainType.Steppe, 0.7f)]
    [DataRow(TerrainType.Swamp, 0.7f)]
    [DataRow(TerrainType.Mountain, 0.5f)]
    [DataRow(TerrainType.Canyon, 0.5f)]
    [DataRow(TerrainType.Snow, 0.5f)]
    [DataRow(TerrainType.Desert, 0.2f)]
    [DataRow(TerrainType.RuralArea, 0.5f)]
    [DataRow(TerrainType.Dune, 0.2f)]
    [DataRow(TerrainType.Lake, 0f)]
    [DataRow(TerrainType.OpenSea, 0f)]
    public void HuntFactor_ByTerrain_IsForestBestAndWaterNone(TerrainType terrain, float expected) =>
        Assert.AreEqual(expected, CampActivityRules.GroundFactor(CampActivity.Hunt, (int)terrain, waterNearby: false), 1e-6f);

    [TestMethod]
    public void TimberFactor_IsForestOnly()
    {
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
        {
            float expected = terrain == TerrainType.Forest ? 1f : 0f;
            Assert.AreEqual(expected, CampActivityRules.GroundFactor(CampActivity.Timber, (int)terrain, waterNearby: true), terrain.ToString());
        }
    }

    [TestMethod]
    public void FishFactor_NeedsWaterNearby_OnAnyGround()
    {
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
        {
            Assert.AreEqual(1f, CampActivityRules.GroundFactor(CampActivity.Fish, (int)terrain, waterNearby: true), terrain.ToString());
            Assert.AreEqual(0f, CampActivityRules.GroundFactor(CampActivity.Fish, (int)terrain, waterNearby: false), terrain.ToString());
        }
    }

    [TestMethod]
    public void EveryTerrainType_IsClassifiedForHunting_AndNothingElseIsInvented()
    {
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
        {
            float factor = CampActivityRules.GroundFactor(CampActivity.Hunt, (int)terrain, waterNearby: false);
            Assert.IsTrue(factor >= 0f && factor <= 1f, terrain + " has a factor outside 0..1");
            Assert.IsTrue(CampActivityRules.IsClassified((int)terrain), terrain + " is not named in the work tables");
        }
    }

    [DataTestMethod]
    [DataRow(999)]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(9)]
    public void AnUnknownTerrain_IsDenied_ForEveryWork(int terrain)
    {
        foreach (var activity in new[] { CampActivity.Hunt, CampActivity.Timber })
            Assert.AreEqual(0f, CampActivityRules.GroundFactor(activity, terrain, waterNearby: false), activity.ToString());
        Assert.IsFalse(CampActivityRules.IsClassified(terrain));
        Assert.IsFalse(CampActivityRules.IsWater(terrain));
    }

    [TestMethod]
    public void TheHuntTable_IsTheOnlyTable_ClassifiedIsExactlyTheNamedTerrains()
    {
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
            Assert.AreEqual(CampActivityRules.HuntFactorOrNull((int)terrain).HasValue, CampActivityRules.IsClassified((int)terrain), terrain.ToString());

        Assert.IsNull(CampActivityRules.HuntFactorOrNull(999));
        Assert.AreEqual(0f, CampActivityRules.HuntFactor(999));
        Assert.AreEqual(0.5f, CampActivityRules.HuntFactorOrNull((int)TerrainType.RuralArea));
        Assert.AreEqual(0f, CampActivityRules.HuntFactorOrNull((int)TerrainType.River), "named, and no game there");
    }

    [TestMethod]
    public void NoActivity_HasNoGroundFactor() =>
        Assert.AreEqual(0f, CampActivityRules.GroundFactor(CampActivity.None, (int)TerrainType.Forest, waterNearby: true));

    [TestMethod]
    public void WaterSet_IsRiverLakeWaterFordCoastBeachBridgeAndNonNavigableRiver()
    {
        var water = Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().Where(t => CampActivityRules.IsWater((int)t)).ToList();

        CollectionAssert.AreEquivalent(new[]
        {
            TerrainType.River, TerrainType.Lake, TerrainType.Water, TerrainType.Fording,
            TerrainType.NonNavigableRiver, TerrainType.CoastalSea, TerrainType.Beach, TerrainType.Bridge,
        }, water);
    }

    // ---- Injury

    [TestMethod]
    public void TheInjuryRoll_FallsDueOnceWhenProgressCrossesTheMidpoint()
    {
        Assert.IsFalse(CampActivityRules.InjuryRollDue(0.1f, 0.49f, alreadyRolled: false));
        Assert.IsTrue(CampActivityRules.InjuryRollDue(0.49f, 0.5f, alreadyRolled: false));
        Assert.IsTrue(CampActivityRules.InjuryRollDue(0.1f, 1f, alreadyRolled: false), "one long step over the midpoint still rolls");
        Assert.IsFalse(CampActivityRules.InjuryRollDue(0.5f, 0.9f, alreadyRolled: false), "already past it");
        Assert.IsFalse(CampActivityRules.InjuryRollDue(0.1f, 1f, alreadyRolled: true), "the saved flag stops a second roll");
        Assert.IsFalse(CampActivityRules.InjuryRollDue(float.NaN, float.NaN, alreadyRolled: false));
    }

    [DataTestMethod]
    [DataRow(0f, true)]
    [DataRow(0.0999f, true)]
    [DataRow(0.1f, false)]
    [DataRow(0.9f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(-1f, false)]
    public void TheInjuryRoll_HitsBelowTenPercent(float roll, bool hit) =>
        Assert.AreEqual(hit, CampActivityRules.InjuryHits(roll));

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(10, 1)]
    [DataRow(20, 1)]
    [DataRow(30, 2)]
    [DataRow(100, 5)]
    [DataRow(-3, 0)]
    public void InjuredMen_AreAboutFivePercentOfHealthyRegulars_AtLeastOne(int regulars, int expected) =>
        Assert.AreEqual(expected, CampActivityRules.InjuredMen(regulars));

    // ---- Morale

    [DataTestMethod]
    [DataRow(0, 0, 0f)]
    [DataRow(10, 0, 0f)]
    [DataRow(5, 0, 0f)]
    [DataRow(20, 0, 1f)]
    [DataRow(25, 0, 1f)]
    [DataRow(60, 0, 5f)]
    [DataRow(60, 2, 4f)]
    [DataRow(60, 6, 2f)]
    [DataRow(60, 10, 0f)]
    [DataRow(60, 99, 0f)]
    [DataRow(60, -3, 5f)]
    public void MoraleCost_OnePerTenMenAboveTen_TimesOneMinusTenPercentPerClanTier(int men, int tier, float expected) =>
        Assert.AreEqual(expected, CampActivityRules.MoraleCost(men, tier), 1e-5f);

    // ---- Tools

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(9, 0)]
    [DataRow(10, 0)]
    [DataRow(11, 2)]
    [DataRow(20, 2)]
    [DataRow(21, 3)]
    [DataRow(100, 10)]
    [DataRow(-5, 0)]
    public void Tools_AreFreeForTenMenOrFewer_ThenOnePerTenStarted(int men, int expected) =>
        Assert.AreEqual(expected, CampActivityRules.ToolsNeeded(men));

    // ---- The saved state

    [DataTestMethod]
    [DataRow(0, 0f, 0f, true)]
    [DataRow(1, 3f, 10f, true)]
    [DataRow(3, 10f, 10f, true)]
    [DataRow(4, 3f, 10f, false)]
    [DataRow(-1, 3f, 10f, false)]
    [DataRow(1, float.NaN, 10f, false)]
    [DataRow(1, -1f, 10f, false)]
    [DataRow(1, 3f, float.NaN, false)]
    [DataRow(1, 3f, 0f, false)]
    [DataRow(1, 3f, -2f, false)]
    [DataRow(1, float.PositiveInfinity, 10f, false)]
    public void ASavedActivity_IsSane_OnlyWhenItsKindAndHoursAreValid(int activity, float done, float target, bool sane) =>
        Assert.AreEqual(sane, CampActivityRules.IsSane(activity, done, target));

    [TestMethod]
    public void Fraction_IsHoursDoneOverTarget_ClampedToZeroAndOne()
    {
        Assert.AreEqual(0.25f, CampActivityRules.Fraction(2.5f, 10f), 1e-6f);
        Assert.AreEqual(1f, CampActivityRules.Fraction(12f, 10f));
        Assert.AreEqual(0f, CampActivityRules.Fraction(-1f, 10f));
        Assert.AreEqual(1f, CampActivityRules.Fraction(1f, 0f), "a degenerate target counts as finished");
        Assert.AreEqual(0f, CampActivityRules.Fraction(float.NaN, 10f));
    }
}
