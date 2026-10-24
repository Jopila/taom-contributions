using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// Two sides are opposed only when both are Free or Evil and they differ. Neutral is nobody's opponent, which is why this is not
/// <c>IAlignmentService.AreEnemyAlignments</c> (that one calls Neutral the enemy of everyone). All nine pairs.
/// </summary>
[TestClass]
public class FactionSideRulesTests
{
    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Free, false)]
    [DataRow(FactionSide.Free, FactionSide.Evil, true)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Evil, FactionSide.Free, true)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, false)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, false)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, false)]
    public void AreOpposed_EachOfTheNinePairs_IsTrueOnlyForFreeAgainstEvil(FactionSide a, FactionSide b, bool expected)
    {
        Assert.AreEqual(expected, FactionSideRules.AreOpposed(a, b));
    }
}
