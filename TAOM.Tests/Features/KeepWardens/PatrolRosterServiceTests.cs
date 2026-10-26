using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The patrol roster fit (#991): a template spawns each stack at its maximum, whatever the patrol's size limit, so the
/// service scales the stack counts to the limit. It is pure: counts in, counts out.
/// </summary>
[TestClass]
public class PatrolRosterServiceTests
{
    private static readonly PatrolRosterService Sut = new();

    [DataTestMethod]
    [DataRow(15, new[] { 9, 4, 2 })]
    [DataRow(25, new[] { 14, 7, 4 })]
    [DataRow(56, new[] { 31, 15, 10 })]
    public void Fit_AnArthedainLikeSixThreeTwo_ScalesToTheLimit_RemainderToTheFirstStacks(int limit, int[] expected)
    {
        var fitted = Sut.Fit(new[] { 6, 3, 2 }, limit);

        CollectionAssert.AreEqual(expected, fitted);
    }

    [TestMethod]
    public void Fit_ARosterOverTheLimit_ScalesDown()
    {
        var fitted = Sut.Fit(new[] { 12, 9, 6 }, 25);

        Assert.AreEqual(25, fitted.Sum());
        Assert.IsTrue(fitted[0] >= fitted[1] && fitted[1] >= fitted[2], "the proportions hold: " + string.Join(",", fitted));
    }

    [TestMethod]
    public void Fit_ARosterAlreadyAtTheLimit_IsUnchanged()
    {
        var counts = new[] { 10, 8, 7 };

        Assert.AreSame(counts, Sut.Fit(counts, 25), "unchanged means the same instance, not a copy");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(int.MinValue)]
    public void Fit_ALimitOfZeroOrLess_PassesTheInputThrough(int limit)
    {
        CollectionAssert.AreEqual(new[] { 6, 3, 2 }, Sut.Fit(new[] { 6, 3, 2 }, limit));
    }

    [TestMethod]
    public void Fit_AnEmptyRoster_PassesThrough()
    {
        Assert.AreEqual(0, Sut.Fit(new int[0], 25).Length);
    }

    [TestMethod]
    public void Fit_AllZeroCounts_PassThrough()
    {
        CollectionAssert.AreEqual(new[] { 0, 0 }, Sut.Fit(new[] { 0, 0 }, 25));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(24)]
    [DataRow(25)]
    [DataRow(57)]
    public void Fit_ASingleStack_TakesTheWholeLimit(int limit)
    {
        CollectionAssert.AreEqual(new[] { limit }, Sut.Fit(new[] { 24 }, limit));
    }

    [TestMethod]
    public void Fit_SumsToTheLimit_AndNeverGoesNegative_ForEveryLimit()
    {
        var shapes = new[]
        {
            new[] { 6, 3, 2 }, new[] { 12, 9, 6 }, new[] { 1, 1, 1, 1, 1, 1, 1 }, new[] { 30, 1 }, new[] { 1, 30 },
            new[] { 5, 5, 5, 5 }, new[] { 24 }, new[] { 0, 5, 3 },
        };

        foreach (var counts in shapes)
        {
            for (var limit = 1; limit <= 120; limit++)
            {
                var fitted = Sut.Fit(counts, limit);

                Assert.AreEqual(limit, fitted.Sum(), $"[{string.Join(",", counts)}] to {limit}");
                Assert.IsTrue(fitted.All(c => c >= 0), $"[{string.Join(",", counts)}] to {limit} went negative");
                Assert.AreEqual(counts.Length, fitted.Length);
            }
        }
    }

    [TestMethod]
    public void Fit_AZeroStackNextToTwoMenStacks_StaysZero_AndTheLimitIsStillMet()
    {
        var fitted = Sut.Fit(new[] { 0, 5, 3 }, 25);

        Assert.AreEqual(0, fitted[0], "a stack with no men never gains any");
        Assert.AreEqual(25, fitted.Sum());
    }

    [TestMethod]
    public void Fit_DoesNotMutateItsInput()
    {
        var counts = new[] { 6, 3, 2 };

        Sut.Fit(counts, 15);

        CollectionAssert.AreEqual(new[] { 6, 3, 2 }, counts);
    }
}
