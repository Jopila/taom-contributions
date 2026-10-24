using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HearthRoll;

namespace TAOM.Tests.Features.HearthRoll;

[TestClass]
public class HearthRollRulesTests
{
    [DataTestMethod]
    [DataRow(true, true, 0, false)]
    [DataRow(true, true, -1, true)]
    [DataRow(true, true, -50, true)]
    [DataRow(true, true, 1, false)]
    [DataRow(true, false, -50, false)]
    [DataRow(false, true, -50, false)]
    [DataRow(false, false, -50, false)]
    public void Tint_NeedsTheToggle_ACompanion_AndARelationBelowZero(bool enabled, bool companion, int relation, bool expected)
        => Assert.AreEqual(expected, HearthRollRules.ShowsUnhappyTint(enabled, companion, relation));

    [DataTestMethod]
    [DataRow(1f, 1)]
    [DataRow(0f, 0)]
    [DataRow(2f, 2)]
    [DataRow(7f, 7)]
    [DataRow(7.9f, 7)]
    [DataRow(0.99f, 0)]
    public void AChild_ShowsItsWholeYears(float age, int expected)
        => Assert.AreEqual(expected, HearthRollRules.AgeYears(isChild: true, age));

    [TestMethod]
    public void ANonChild_HasNoAge()
    {
        Assert.IsNull(HearthRollRules.AgeYears(isChild: false, 7f));
        Assert.IsNull(HearthRollRules.AgeYears(isChild: false, 30f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.5f)]
    [DataRow(-3f)]
    public void ABadAge_HasNoLabel(float age)
        => Assert.IsNull(HearthRollRules.AgeYears(isChild: true, age));
}
