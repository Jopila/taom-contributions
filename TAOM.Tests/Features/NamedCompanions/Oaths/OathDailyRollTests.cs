using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class OathDailyRollTests
{
    [TestMethod]
    public void Percent_SameInputs_SameValue()
    {
        Assert.AreEqual(OathDailyRoll.Percent("c", "t", 12), OathDailyRoll.Percent("c", "t", 12));
    }

    [TestMethod]
    public void Percent_IsPinnedToFnv1a_SoItSurvivesARuntimeChange()
    {
        // string.GetHashCode differs per runtime; the roll must not.
        Assert.AreEqual(Expected("a|b|1"), OathDailyRoll.Percent("a", "b", 1));
        Assert.AreEqual(Expected("named_companion_gimli|ask|4321"), OathDailyRoll.Percent("named_companion_gimli", "ask", 4321));
    }

    [TestMethod]
    public void Percent_StaysInZeroTo99_AndVariesByDay()
    {
        var rolls = Enumerable.Range(0, 2000).Select(d => OathDailyRoll.Percent("c", "t", d)).ToList();

        Assert.IsTrue(rolls.All(r => r >= 0 && r <= 99));
        Assert.IsTrue(rolls.Distinct().Count() > 50);
    }

    [TestMethod]
    public void Percent_BelowThirty_IsAboutThirtyPercentOverTwoThousandDays()
    {
        var hits = Enumerable.Range(0, 2000).Count(d => OathDailyRoll.Percent("named_companion_gimli", "ask", d) < 30);

        Assert.IsTrue(hits >= 500 && hits <= 700, "hits " + hits);
    }

    [TestMethod]
    public void Percent_DifferentCharacterOrTopic_RollsIndependently()
    {
        var days = Enumerable.Range(0, 200).ToList();

        Assert.IsTrue(days.Any(d => OathDailyRoll.Percent("c1", "t", d) != OathDailyRoll.Percent("c2", "t", d)));
        Assert.IsTrue(days.Any(d => OathDailyRoll.Percent("c", "t1", d) != OathDailyRoll.Percent("c", "t2", d)));
    }

    private static int Expected(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return (int)(hash % 100);
    }
}
