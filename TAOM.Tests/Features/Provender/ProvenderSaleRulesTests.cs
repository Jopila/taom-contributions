using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;

namespace TAOM.Tests.Features.Provender;

/// <summary>The arithmetic of remote selling: the fee curve, the fee price, the town ranking and the town's gold change.</summary>
[TestClass]
public class ProvenderSaleRulesTests
{
    [DataTestMethod]
    [DataRow(0, 0, 30)]
    [DataRow(125, 0, 20)]
    [DataRow(250, 0, 10)]
    [DataRow(300, 0, 10)]
    [DataRow(-40, 0, 30)]
    [DataRow(0, 1, 35)]
    [DataRow(250, 2, 20)]
    public void FeePercent_FallsWithTrade_AndRisesFivePerRank(int trade, int rank, int expected)
    {
        Assert.AreEqual(expected, ProvenderSaleRules.FeePercent(trade, 30, 10, rank));
    }

    [TestMethod]
    public void FeePercent_IsClampedToZeroAndNinetyFive()
    {
        Assert.AreEqual(95, ProvenderSaleRules.FeePercent(0, 90, 90, 4));
        Assert.AreEqual(0, ProvenderSaleRules.FeePercent(250, 0, 0, 0));
        Assert.AreEqual(30, ProvenderSaleRules.FeePercent(0, 30, 10, -3), "a negative rank adds nothing");
    }

    [DataTestMethod]
    [DataRow(100, 30, 70)]
    [DataRow(100, 0, 100)]
    [DataRow(1, 30, 1)]
    [DataRow(3, 95, 1)]
    [DataRow(0, 30, 0)]
    [DataRow(int.MaxValue, 50, 1073741823)]
    public void FeePrice_TakesTheFeeOff_AndNeverGoesBelowOne(int price, int fee, int expected)
    {
        Assert.AreEqual(expected, ProvenderSaleRules.FeePrice(price, fee));
    }

    private static SaleTownFacts Town(string id, float distance, bool eligible = true) =>
        new() { Id = id, Name = id, Distance = distance, Gold = eligible ? 10000 : 0, Side = SideIds.None };

    [TestMethod]
    public void Rank_FiltersBeforeTakingN_SoAnIneligibleTownWastesNoSlot()
    {
        var towns = new[] { Town("near_bad", 1f, eligible: false), Town("a", 2f), Town("b", 3f), Town("c", 4f) };

        var ranked = ProvenderSaleRules.Rank(towns, t => t.Gold > 0, 2);

        CollectionAssert.AreEqual(new[] { "a", "b" }, ranked.Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void Rank_DropsADistanceTheEngineCouldNotMeasure()
    {
        // Unreachable comes back as a huge finite placeholder (1e30 native, 1e8, float.MaxValue); 0 or less is no road either.
        var towns = new[]
        {
            Town("nan", float.NaN), Town("neg", -1f), Town("inf", float.PositiveInfinity), Town("zero", 0f),
            Town("native", 1e30f), Town("placeholder", 1e8f), Town("max", float.MaxValue), Town("edge", ProvenderSaleRules.UnreachableDistance),
            Town("ok", 5f),
        };

        CollectionAssert.AreEqual(new[] { "ok" }, ProvenderSaleRules.Rank(towns, _ => true, 5).Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void Rank_TiesGoToTheIdThatSortsFirst_AndNLargerThanTheTownsTakesThemAll()
    {
        var towns = new[] { Town("town_b", 3f), Town("town_a", 3f) };

        CollectionAssert.AreEqual(new[] { "town_a", "town_b" }, ProvenderSaleRules.Rank(towns, _ => true, 9).Select(t => t.Id).ToArray());
        Assert.AreEqual(0, ProvenderSaleRules.Rank(towns, _ => true, 0).Count);
    }

    [DataTestMethod]
    [DataRow(true, 500, 10000, 0)]
    [DataRow(false, 500, 10000, -500)]
    [DataRow(false, 500, 300, -300)]
    [DataRow(false, -200, 10000, 0)]
    [DataRow(false, 0, 10000, 0)]
    public void TownGoldChange_IsNothingWhenTheEngineSettled_ElseTheOppositeOfThePlayersGain(bool settled, int gain, int townGold, int expected)
    {
        Assert.AreEqual(expected, ProvenderSaleRules.TownGoldChange(settled, gain, townGold));
    }
}
