using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class KeptPairFinderTests
{
    private static IReadOnlyDictionary<string, string> Rows(params (string Id, string Text)[] rows) =>
        rows.ToDictionary(r => r.Id, r => r.Text);

    [TestMethod]
    public void Find_IdInBothFiles_ReturnsAPairWithBothTexts()
    {
        var pairs = KeptPairFinder.Find(Rows(("a", "taom a")), Rows(("a", "vanilla a")));
        Assert.AreEqual(1, pairs.Count);
        Assert.AreEqual("a", pairs[0].Id);
        Assert.AreEqual("taom a", pairs[0].TaomText);
        Assert.AreEqual("vanilla a", pairs[0].VanillaText);
    }

    [TestMethod]
    public void Find_TaomOnlyId_ReturnsNoPair()
    {
        Assert.AreEqual(0, KeptPairFinder.Find(Rows(("taom_x", "x")), Rows(("a", "a"))).Count);
    }

    [TestMethod]
    public void Find_VanillaOnlyId_ReturnsNoPair()
    {
        Assert.AreEqual(0, KeptPairFinder.Find(Rows(("b", "b")), Rows(("a", "a"))).Count);
    }

    [TestMethod]
    public void Find_SameText_ReturnsNoPair()
    {
        Assert.AreEqual(0, KeptPairFinder.Find(Rows(("a", "same")), Rows(("a", "same"))).Count);
    }

    [TestMethod]
    public void Find_SeveralPairs_AreSortedById()
    {
        var pairs = KeptPairFinder.Find(Rows(("z", "1"), ("a", "1"), ("m", "1")), Rows(("m", "2"), ("a", "2"), ("z", "2")));
        CollectionAssert.AreEqual(new[] { "a", "m", "z" }, pairs.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public void Find_BlankTaomOrVanillaRow_IsSkipped()
    {
        Assert.AreEqual(0, KeptPairFinder.Find(Rows(("a", "")), Rows(("a", "v"))).Count);
        Assert.AreEqual(0, KeptPairFinder.Find(Rows(("a", "t")), Rows(("a", " "))).Count);
    }
}
