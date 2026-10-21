using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// The side pairing, pinned for the nine ordered pairings of three sides. The multipliers are 1, 0.75 and 0.5, all different, so a
/// wrong pairing returns the wrong number.
/// </summary>
[TestClass]
public class SideMultipliersTests
{
    private static readonly SideMultipliers Multipliers = new(1f, 0.75f, 0.5f);

    [DataTestMethod]
    [DataRow(FactionSide.Free, FactionSide.Free, 1f)]
    [DataRow(FactionSide.Evil, FactionSide.Evil, 1f)]
    [DataRow(FactionSide.Free, FactionSide.Evil, 0.5f)]
    [DataRow(FactionSide.Evil, FactionSide.Free, 0.5f)]
    [DataRow(FactionSide.Free, FactionSide.Neutral, 0.75f)]
    [DataRow(FactionSide.Neutral, FactionSide.Free, 0.75f)]
    [DataRow(FactionSide.Neutral, FactionSide.Evil, 0.75f)]
    [DataRow(FactionSide.Evil, FactionSide.Neutral, 0.75f)]
    [DataRow(FactionSide.Neutral, FactionSide.Neutral, 0.75f)]
    public void For_NeutralOnEitherSideIsNeutral_OtherwiseSameOrOpposite(FactionSide first, FactionSide second, float expected)
    {
        Assert.AreEqual(expected, Multipliers.For(first, second));
    }

    [TestMethod]
    public void For_EachOfTheNineOrderedPairingsOfThreeSides_IsPinnedByARowAbove()
    {
        // The docs say "nine pairings": three sides, ordered. The rows above must name every one of them once.
        var rows = typeof(SideMultipliersTests)
            .GetMethod(nameof(For_NeutralOnEitherSideIsNeutral_OtherwiseSameOrOpposite))!
            .GetCustomAttributes(typeof(DataRowAttribute), inherit: false)
            .Cast<DataRowAttribute>()
            .Select(r => ((FactionSide)r.Data[0]!, (FactionSide)r.Data[1]!))
            .ToList();
        var sides = new[] { FactionSide.Free, FactionSide.Evil, FactionSide.Neutral };
        var all = sides.SelectMany(a => sides.Select(b => (a, b))).ToList();

        Assert.AreEqual(9, all.Count);
        CollectionAssert.AreEquivalent(all, rows);
    }
}
