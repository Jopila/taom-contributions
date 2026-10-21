using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>The tribute envoy's pure arithmetic (#870 slice 2): price, haggle, timers, the grudge and the spared clan.</summary>
[TestClass]
public class FireAndTributeEnvoyMathTests
{
    private static readonly FactionSide[] FreeOnly = { FactionSide.Free };

    private static TargetCandidate K(string id, FactionSide side, float strength) => new(id, side, strength);

    // ---- price units -----------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 1)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    [DataRow(4, 2)]
    [DataRow(5, 3)]
    [DataRow(6, 3)]
    [DataRow(7, 4)]
    [DataRow(8, 4)]
    [DataRow(9, 5)]
    [DataRow(-5, 1)]
    public void TributeUnits_IsVillagesPlusOneHalved_WithIntegerDivision_AtLeastOne(int villages, int expected)
    {
        Assert.AreEqual(expected, FireAndTributeMath.TributeUnits(villages));
    }

    [TestMethod]
    public void TributeUnits_TheLargestCount_DoesNotOverflow()
    {
        Assert.AreEqual(1073741824, FireAndTributeMath.TributeUnits(int.MaxValue));
    }

    // ---- price -----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TributePrice_IsUnitsTimesUnitPrice_WhenNoEnvoyWasSeized()
    {
        Assert.AreEqual(500, FireAndTributeMath.TributePrice(1, 500, 0));
        Assert.AreEqual(1500, FireAndTributeMath.TributePrice(3, 500, 0));
    }

    [TestMethod]
    public void TributePrice_EachSeizedEnvoy_AddsOneMoreFullPrice()
    {
        Assert.AreEqual(1000, FireAndTributeMath.TributePrice(1, 500, 1));
        Assert.AreEqual(4500, FireAndTributeMath.TributePrice(3, 500, 2));
    }

    [TestMethod]
    public void TributePrice_ANegativeSeizedCount_CountsAsNone()
    {
        Assert.AreEqual(500, FireAndTributeMath.TributePrice(1, 500, -3));
    }

    [TestMethod]
    public void TributePrice_AHugeProduct_IsCappedAtIntMax_NeverWrapped()
    {
        Assert.AreEqual(int.MaxValue, FireAndTributeMath.TributePrice(1073741824, 1000000, int.MaxValue));
    }

    [TestMethod]
    public void TributePrice_NeverFree()
    {
        Assert.AreEqual(1, FireAndTributeMath.TributePrice(0, 500, 0));
        Assert.AreEqual(1, FireAndTributeMath.TributePrice(1, 0, 0));
        Assert.AreEqual(1, FireAndTributeMath.TributePrice(1, -50, 0));
    }

    // ---- haggle ----------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(3, false, false)]
    [DataRow(4, false, true)]
    [DataRow(9, false, true)]
    [DataRow(4, true, false)]
    [DataRow(1, true, false)]
    public void CanHaggle_NeedsFourUnitsAndNoEarlierTry(int units, bool haggled, bool expected)
    {
        Assert.AreEqual(expected, FireAndTributeMath.CanHaggle(units, haggled));
    }

    [DataTestMethod]
    [DataRow(0, 0.20f)]
    [DataRow(100, 0.40f)]
    [DataRow(200, 0.60f)]
    [DataRow(250, 0.70f)]
    [DataRow(1000, 0.70f)]
    [DataRow(-40, 0.20f)]
    public void HaggleChance_IsBasePlusPerPointOfCharm_ClampedToTheBounds(int charm, float expected)
    {
        Assert.AreEqual(expected, FireAndTributeMath.HaggleChance(charm, 0.20f, 0.002f, 0.10f, 0.70f), 0.0001f);
    }

    [TestMethod]
    public void HaggleChance_ALowBase_IsLiftedToTheMinimum()
    {
        Assert.AreEqual(0.10f, FireAndTributeMath.HaggleChance(0, 0.01f, 0.002f, 0.10f, 0.70f), 0.0001f);
    }

    [TestMethod]
    public void HaggleChance_ANonFiniteInput_IsNoChanceAtAll()
    {
        Assert.AreEqual(0f, FireAndTributeMath.HaggleChance(50, float.NaN, 0.002f, 0.1f, 0.7f));
        Assert.AreEqual(0f, FireAndTributeMath.HaggleChance(50, 0.2f, float.PositiveInfinity, 0.1f, 0.7f));
        Assert.AreEqual(0f, FireAndTributeMath.HaggleChance(50, 0.2f, 0.002f, float.NaN, 0.7f));
        Assert.AreEqual(0f, FireAndTributeMath.HaggleChance(50, 0.2f, 0.002f, 0.1f, float.NaN));
    }

    [DataTestMethod]
    [DataRow(100, 75)]
    [DataRow(101, 76)]
    [DataRow(102, 77)]
    [DataRow(103, 78)]
    [DataRow(4, 3)]
    [DataRow(3, 3)]
    [DataRow(2, 2)]
    [DataRow(1, 1)]
    [DataRow(0, 0)]
    [DataRow(-8, 0)]
    public void HaggledPrice_IsThreeQuartersRoundedUp(int price, int expected)
    {
        Assert.AreEqual(expected, FireAndTributeMath.HaggledPrice(price));
    }

    [TestMethod]
    public void HaggledPrice_TheLargestPrice_DoesNotOverflow()
    {
        Assert.AreEqual(1610612736, FireAndTributeMath.HaggledPrice(int.MaxValue));
    }

    // ---- timers ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void DueHour_IsNowPlusTheWait()
    {
        Assert.AreEqual(124, FireAndTributeMath.DueHour(100, 24));
    }

    [TestMethod]
    public void DueHour_NeverOverflows_AndNeverGoesBackwards()
    {
        Assert.AreEqual(int.MaxValue, FireAndTributeMath.DueHour(int.MaxValue - 1, 48));
        Assert.AreEqual(100, FireAndTributeMath.DueHour(100, -5));
    }

    // ---- the grudge ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void PickTarget_AGrudge_BeatsThePreferredListAndTheStrongest()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 300f), K("gondor", FactionSide.Free, 800f), K("erebor", FactionSide.Free, 100f) };

        Assert.AreEqual("rohan", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "erebor" }, "rohan"));
    }

    [TestMethod]
    public void PickTarget_AGrudgeAgainstAKingdomNotInThePool_FallsBackToThePreferredList()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 300f), K("gondor", FactionSide.Free, 800f), K("mordor", FactionSide.Evil, 9000f) };

        Assert.AreEqual("rohan", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "rohan" }, "mordor"));
        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new string[0], "mordor"));
        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new string[0], "dead_kingdom"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void PickTarget_NoGrudge_IsTheOldOrder(string? grudge)
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 300f), K("gondor", FactionSide.Free, 800f) };

        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new string[0], grudge));
        Assert.AreEqual("rohan", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "rohan" }, grudge));
    }

    // ---- the spared clan -------------------------------------------------------------------------------------------

    private static VillageReading Owned(string id, string? clan, float x = 0f) =>
        new VillageReading(true, "gondor", true, 400f, id, x, 0f, clan);

    [TestMethod]
    public void IsRaidable_AVillageOfTheSparedClan_IsNotRaidable()
    {
        Assert.IsFalse(FireAndTributeMath.IsRaidable(Owned("v", "player_clan"), "gondor", 150f, "player_clan"));
    }

    [TestMethod]
    public void IsRaidable_AVillageOfAnotherClan_OrWithNoSparedClan_StaysRaidable()
    {
        Assert.IsTrue(FireAndTributeMath.IsRaidable(Owned("v", "clan_other"), "gondor", 150f, "player_clan"));
        Assert.IsTrue(FireAndTributeMath.IsRaidable(Owned("v", "player_clan"), "gondor", 150f, null));
        Assert.IsTrue(FireAndTributeMath.IsRaidable(Owned("v", "player_clan"), "gondor", 150f));
    }

    [TestMethod]
    public void IsRaidable_AVillageWithNoReadableOwnerClan_IsRaidableEvenWithASparedClan()
    {
        Assert.IsTrue(FireAndTributeMath.IsRaidable(Owned("v", null), "gondor", 150f, "player_clan"));
    }

    [TestMethod]
    public void NearestRaidable_SkipsTheSparedClansVillages_AndTakesTheNextNearest()
    {
        var villages = new[] { Owned("near", "player_clan", 1f), Owned("far", "clan_other", 5f), Owned("farther", "clan_other", 9f) };

        var picked = FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 2, "player_clan");

        CollectionAssert.AreEqual(new[] { "far", "farther" }, picked.ToArray());
    }

    [TestMethod]
    public void NearestRaidable_WithoutASparedClan_IsUnchanged()
    {
        var villages = new[] { Owned("near", "player_clan", 1f), Owned("far", "clan_other", 5f) };

        CollectionAssert.AreEqual(new[] { "near", "far" }, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 2).ToArray());
    }

    [TestMethod]
    public void KingdomsWithRaidable_IsNotAffectedByTheOwner()
    {
        var villages = new[] { Owned("v", "player_clan") };

        Assert.IsTrue(FireAndTributeMath.KingdomsWithRaidable(villages, 150f).Contains("gondor"));
    }
}
