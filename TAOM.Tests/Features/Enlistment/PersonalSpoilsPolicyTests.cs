using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#834: which kills are tried for a spoil, in what order, and at what target value.</summary>
[TestClass]
public class PersonalSpoilsPolicyTests
{
    private static EnlistmentContentConfig Config(double scale = 0.25, double min = 0.1, double max = 0.9)
    {
        var c = EnlistmentContentConfigProvider.BuildDefaults();
        c.SpoilsValueScalePerRank = scale;
        c.SpoilsRandomMin = min;
        c.SpoilsRandomMax = max;
        return c;
    }

    private static Func<string, float?> Values(params (string id, float? v)[] rows)
    {
        var d = rows.ToDictionary(r => r.id, r => r.v);
        return id => d.TryGetValue(id, out var v) ? v : null;
    }

    [TestMethod]
    public void Rank_OrdersByExpectedValueDescending_WithNoCap()
    {
        var picks = PersonalSpoilsPolicy.Rank(
            new[] { "a", "b", "c", "d", "e" },
            Values(("a", 100f), ("b", 400f), ("c", 300f), ("d", 200f), ("e", 50f)),
            0, Config(), () => 0.5);

        CollectionAssert.AreEqual(new[] { "b", "c", "d", "a", "e" }, picks.Select(p => p.CharacterId).ToArray());
    }

    [TestMethod]
    public void Rank_EqualValues_KeepKillOrder()
    {
        var picks = PersonalSpoilsPolicy.Rank(
            new[] { "x", "y", "z" }, Values(("x", 80f), ("y", 80f), ("z", 80f)), 0, Config(), () => 0.5);

        CollectionAssert.AreEqual(new[] { "x", "y", "z" }, picks.Select(p => p.CharacterId).ToArray());
    }

    [TestMethod]
    public void Rank_TargetIsExpectedTimesRankScaleTimesRandomFactor()
    {
        // rank 2, scale 0.25 -> x1.5; random 0.5 -> factor 0.1 + 0.5 * 0.8 = 0.5
        var picks = PersonalSpoilsPolicy.Rank(new[] { "a" }, Values(("a", 200f)), 2, Config(), () => 0.5);

        Assert.AreEqual(1, picks.Count);
        Assert.AreEqual(200f * 1.5f * 0.5f, picks[0].TargetValue, 0.001f);
    }

    [DataTestMethod]
    [DataRow(0.0, 0.1)]
    [DataRow(1.0, 0.9)]
    public void Rank_RandomBandEdgesLandOnMinAndMax(double random, double expectedFactor)
    {
        var picks = PersonalSpoilsPolicy.Rank(new[] { "a" }, Values(("a", 100f)), 0, Config(), () => random);

        Assert.AreEqual(100f * (float)expectedFactor, picks[0].TargetValue, 0.001f);
    }

    [TestMethod]
    public void Rank_RankZeroIsNotScaled()
    {
        var picks = PersonalSpoilsPolicy.Rank(new[] { "a" }, Values(("a", 100f)), 0, Config(min: 1.0, max: 1.0), () => 0.3);

        Assert.AreEqual(100f, picks[0].TargetValue, 0.001f);
    }

    [TestMethod]
    public void Rank_SkipsEmptyIdsAndNullNonFiniteNonPositiveValues()
    {
        var picks = PersonalSpoilsPolicy.Rank(
            new[] { "", "nul", "nan", "inf", "zero", "neg", "ok" },
            Values(("nul", null), ("nan", float.NaN), ("inf", float.PositiveInfinity), ("zero", 0f), ("neg", -5f), ("ok", 10f)),
            0, Config(), () => 0.5);

        CollectionAssert.AreEqual(new[] { "ok" }, picks.Select(p => p.CharacterId).ToArray());
    }

    [TestMethod]
    public void Rank_AllInvalidOrNoKills_IsEmpty()
    {
        Assert.AreEqual(0, PersonalSpoilsPolicy.Rank(new[] { "a", "b" }, Values(("a", null), ("b", 0f)), 0, Config(), () => 0.5).Count);
        Assert.AreEqual(0, PersonalSpoilsPolicy.Rank(new string[0], Values(), 0, Config(), () => 0.5).Count);
        Assert.AreEqual(0, PersonalSpoilsPolicy.Rank(null, Values(), 0, Config(), () => 0.5).Count);
    }

    [TestMethod]
    public void Rank_TheSameKindKilledSeveralTimes_IsListedEachTime()
    {
        var picks = PersonalSpoilsPolicy.Rank(new[] { "orc", "orc", "orc", "orc" }, Values(("orc", 80f)), 0, Config(), () => 0.5);

        Assert.AreEqual(4, picks.Count);
    }
}
