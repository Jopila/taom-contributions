using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Domain;
using TAOM.Core.Validation;
using TAOM.Features.UnderTheEaves;

namespace TAOM.Tests.Features.UnderTheEaves;

/// <summary>
/// #849: the rule is three facts in, one sight factor out. Pure, so it runs anywhere. That the engine's forest terrain is what the
/// adapter compares against, and that Keen Sight is the forest perk that halves a penalty, are pinned against the installed
/// assemblies in UnderTheEavesEngineFactsTests.
/// </summary>
[TestClass]
public class UnderTheEavesRuleTests
{
    /// <summary>The full truth table: inForest, inSettlement, keenSight, then the factor.</summary>
    [DataTestMethod]
    [DataRow(false, false, false, 0f)]
    [DataRow(false, false, true, 0f)]
    [DataRow(false, true, false, 0f)]
    [DataRow(false, true, true, 0f)]
    [DataRow(true, false, false, -0.3f)]
    [DataRow(true, false, true, -0.15f)]
    [DataRow(true, true, false, 0f)]
    [DataRow(true, true, true, 0f)]
    public void Factor_TruthTable(bool inForest, bool inSettlement, bool keenSight, float expected) =>
        Assert.AreEqual(expected, UnderTheEavesRule.Factor(inForest, inSettlement, keenSight), 0.00001f);

    [TestMethod]
    public void ForestPenalty_IsFinite_NegativeAndInsideTheComposersPenaltyBudget()
    {
        // No float reaches config, so the one number a player cannot change is pinned to the range the composer accepts,
        // with room left under the -0.5 floor for Far Watch's settlement ride.
        Assert.IsTrue(FiniteFloatValidator.IsFinite(UnderTheEavesRule.ForestPenalty));
        Assert.IsTrue(SpottingContributorComposer.MaxPenalty < UnderTheEavesRule.ForestPenalty && UnderTheEavesRule.ForestPenalty < 0f,
            "a penalty that never reaches the floor on its own");
    }

    [TestMethod]
    public void KeenSightShare_IsFinite_AndHalvesThePenalty()
    {
        Assert.IsTrue(FiniteFloatValidator.IsFinite(UnderTheEavesRule.KeenSightShare));
        Assert.AreEqual(0.5f, UnderTheEavesRule.KeenSightShare, "vanilla's Keen Sight PrimaryBonus is -0.5, so the penalty keeps half");
    }
}
