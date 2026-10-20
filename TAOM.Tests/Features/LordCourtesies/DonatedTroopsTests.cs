using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LordCourtesies;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Tests.Features.LordCourtesies;

/// <summary>The roster diff. What the troops are worth is the engine's number, so there is no formula to test.</summary>
[TestClass]
public class DonatedTroopsTests
{
    private static TroopCount T(string id, int count, bool hero = false) => new(id, count, hero);

    private static List<TroopCount> L(params TroopCount[] stacks) => stacks.ToList();

    private static IReadOnlyList<TroopCount> Diff(List<TroopCount> before, List<TroopCount> after) =>
        LordCourtesiesService.Donated(before, after);

    [TestMethod]
    public void Donated_CountsOnlyTheIncreasePerCharacter()
    {
        var donated = Diff(L(T("a", 10), T("b", 5)), L(T("a", 14), T("b", 5)));

        Assert.AreEqual(1, donated.Count);
        Assert.AreEqual("a", donated[0].CharacterId);
        Assert.AreEqual(4, donated[0].Count);
    }

    [TestMethod]
    public void Donated_ANewStack_CountsInFull()
    {
        var donated = Diff(L(T("a", 10)), L(T("a", 10), T("c", 6)));

        Assert.AreEqual(1, donated.Count);
        Assert.AreEqual("c", donated[0].CharacterId);
        Assert.AreEqual(6, donated[0].Count);
    }

    [TestMethod]
    public void Donated_ADecreaseOrNoChange_IsNotADonation()
    {
        Assert.AreEqual(0, Diff(L(T("a", 10), T("b", 5)), L(T("a", 7), T("b", 5))).Count);
    }

    [TestMethod]
    public void Donated_HeroesAreIgnored()
    {
        Assert.AreEqual(0, Diff(L(T("a", 10)), L(T("a", 10), T("hero", 1, hero: true))).Count);
    }

    [TestMethod]
    public void Donated_TheSameCharacterInSeveralRosterElements_IsFoldedBeforeTheDiff()
    {
        var donated = Diff(L(T("a", 4), T("a", 4)), L(T("a", 6), T("a", 6)));

        Assert.AreEqual(1, donated.Count);
        Assert.AreEqual(4, donated[0].Count);
    }

    [TestMethod]
    public void Donated_AnEmptyReading_IsNoDonation()
    {
        Assert.AreEqual(0, Diff(L(), L()).Count);
        Assert.AreEqual(0, Diff(L(T("a", 3)), L()).Count);
    }
}
