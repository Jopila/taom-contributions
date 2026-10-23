using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LinesOfDescent;

namespace TAOM.Tests.Features.LinesOfDescent;

/// <summary>Lines of Descent (#905): the root climb, the tree and the relation phrases, against a hand-built graph.</summary>
[TestClass]
public class FamilyTreeServiceTests
{
    private FakeFamilyGraph _g = null!;
    private FamilyTreeService _s = null!;

    [TestInitialize]
    public void SetUp()
    {
        _g = new FakeFamilyGraph();
        _s = new FamilyTreeService(_g);
    }

    // ---- root -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Root_NoParents_IsTheSelectedHero()
    {
        _g.Add("a");
        Assert.AreEqual("a", _s.FindRoot("a"));
    }

    [TestMethod]
    public void Root_ClimbsToTheTopmostListedParentWithAClan()
    {
        _g.Add("gf");
        _g.Add("f", father: "gf");
        _g.Add("a", father: "f");
        Assert.AreEqual("gf", _s.FindRoot("a"));
    }

    [TestMethod]
    public void Root_EachTierBeatsTheNext()
    {
        var setups = new List<System.Action<FakeFamilyGraph.Person>>
        {
            p => { p.KingdomLeader = true; p.Kingdom = "k"; },
            p => { p.Ruling = true; p.Kingdom = "k"; },
            p => { p.ClanLeader = true; p.Kingdom = "k"; },
            p => { p.Kingdom = "k"; },
            p => { p.ClanLeader = true; p.Minor = true; },
            p => { p.Minor = true; },
            p => { p.ClanLeader = true; },
            p => { },
        };
        for (var i = 0; i < setups.Count - 1; i++)
        {
            var g = new FakeFamilyGraph();
            var winner = g.Add("m", female: true);
            var loser = g.Add("f");
            setups[i](winner);
            setups[i + 1](loser);
            g.Add("a", father: "f", mother: "m");
            Assert.AreEqual("m", new FamilyTreeService(g).FindRoot("a"), "tier " + (i + 1) + " must beat tier " + (i + 2));
        }
    }

    [TestMethod]
    public void Root_FatherWinsATie()
    {
        _g.Add("f");
        _g.Add("m", female: true);
        _g.Add("a", father: "f", mother: "m");
        Assert.AreEqual("f", _s.FindRoot("a"));
    }

    [TestMethod]
    public void Root_StopsAtAClanlessParent()
    {
        _g.Add("gf");
        _g.Add("f", father: "gf").Clan = null;
        _g.Add("a", father: "f");
        Assert.AreEqual("a", _s.FindRoot("a"));
    }

    [TestMethod]
    public void Root_StopsAtAnUnlistedParent()
    {
        _g.Add("gf");
        _g.Add("f", father: "gf").Listed = false;
        _g.Add("a", father: "f");
        Assert.AreEqual("a", _s.FindRoot("a"));
    }

    [TestMethod]
    public void Root_AParentCycleEnds()
    {
        _g.Add("a");
        _g.Add("b", father: "a");
        _g.People["a"].Father = "b";
        var root = _s.FindRoot("a");
        Assert.IsTrue(root == "a" || root == "b");
    }

    [TestMethod]
    public void Root_ClimbIsCappedAt32Steps()
    {
        _g.Add("h0");
        for (var i = 1; i <= 40; i++)
            _g.Add("h" + i, father: "h" + (i - 1));
        Assert.AreEqual("h8", _s.FindRoot("h40"));
    }

    [TestMethod]
    public void Root_UnknownHero_IsReturnedAsItself()
        => Assert.AreEqual("nobody", _s.FindRoot("nobody"));

    // ---- tree -------------------------------------------------------------------------------------------------

    private static IEnumerable<string> Ids(FamilyTreeNode n) => n.Members.Select(m => m.HeroId);

    [TestMethod]
    public void Tree_EmptyFamily_IsOneNode()
    {
        _g.Add("a");
        var tree = _s.Build("a")!;
        Assert.AreEqual("a", tree.RootId);
        CollectionAssert.AreEqual(new[] { "a" }, Ids(tree.Root).ToArray());
        Assert.AreEqual(0, tree.Root.Children.Count);
        Assert.AreEqual(1, tree.CardCount);
        Assert.IsTrue(tree.Root.Members[0].IsSelected);
        Assert.IsNull(tree.Root.Members[0].Phrase);
    }

    [TestMethod]
    public void Tree_UnknownHero_IsNull()
        => Assert.IsNull(_s.Build("nobody"));

    [TestMethod]
    public void Tree_DrawsChildrenOnly_AuntsAreNotDrawn()
    {
        _g.Add("gf");
        _g.Add("aunt", father: "gf", female: true);
        _g.Add("f", father: "gf");
        _g.Add("a", father: "f");
        var tree = _s.Build("a")!;
        Assert.AreEqual("gf", tree.RootId);
        var all = Flatten(tree.Root).ToList();
        CollectionAssert.Contains(all, "f");
        CollectionAssert.Contains(all, "a");
        CollectionAssert.Contains(all, "aunt", "the aunt is a child of the root, so she is a line of descent");
    }

    [TestMethod]
    public void Tree_ASpouseParentLineIsNotClimbed()
    {
        _g.Add("sf");
        _g.Add("s", female: true, father: "sf");
        _g.Add("a");
        _g.Marry("a", "s");
        var tree = _s.Build("a")!;
        CollectionAssert.DoesNotContain(Flatten(tree.Root).ToList(), "sf");
        CollectionAssert.AreEqual(new[] { "a", "s" }, Ids(tree.Root).ToArray());
    }

    private static IEnumerable<string> Flatten(FamilyTreeNode n)
    {
        foreach (var m in n.Members) yield return m.HeroId;
        foreach (var c in n.Children)
            foreach (var id in Flatten(c)) yield return id;
    }

    [TestMethod]
    public void Tree_CurrentSpouseFirst_ThenExSpousesInOrder_DuplicatesAndSelfRemoved()
    {
        _g.Add("a");
        _g.Add("w1", female: true);
        _g.Add("w2", female: true);
        _g.Add("w3", female: true);
        _g.Marry("a", "w1");
        _g.Divorce("a", "w1");
        _g.Marry("a", "w2");
        _g.Divorce("a", "w2");
        _g.Marry("a", "w1");
        _g.People["a"].Ex.Add("a");
        _g.People["a"].Ex.Add("w3");
        var row = _s.Build("a")!.Root;
        CollectionAssert.AreEqual(new[] { "a", "w1", "w2", "w3" }, Ids(row).ToArray());
        Assert.IsFalse(row.Members[1].IsExSpouse);
        Assert.IsTrue(row.Members[2].IsExSpouse);
        Assert.IsTrue(row.Members[3].IsExSpouse);
    }

    [TestMethod]
    public void Tree_ChildrenInBirthOrder_TiesByStringId()
    {
        _g.Add("r");
        _g.Add("c3", father: "r", birth: 30);
        _g.Add("c1", father: "r", birth: 10);
        _g.Add("cb", father: "r", birth: 20);
        _g.Add("ca", father: "r", birth: 20);
        var kids = _s.Build("r")!.Root.Children.Select(c => c.Members[0].HeroId).ToArray();
        CollectionAssert.AreEqual(new[] { "c1", "ca", "cb", "c3" }, kids);
    }

    [TestMethod]
    public void Tree_DuplicateChildEntries_AreDrawnOnce()
    {
        _g.Add("r");
        _g.Add("c", father: "r");
        _g.People["r"].Kids.Add("c");
        Assert.AreEqual(1, _s.Build("r")!.Root.Children.Count);
    }

    [TestMethod]
    public void Tree_StaleChild_WhoseParentsAreSomeoneElse_IsDropped()
    {
        _g.Add("r");
        _g.Add("o");
        _g.Add("c", father: "o");
        _g.People["r"].Kids.Add("c");
        Assert.AreEqual(0, _s.Build("r")!.Root.Children.Count);
    }

    [TestMethod]
    public void Tree_UnlistedChildAndUnlistedSpouse_AreSkipped()
    {
        _g.Add("r");
        _g.Add("hid", father: "r").Listed = false;
        _g.Add("sp", female: true).Listed = false;
        _g.Marry("r", "sp");
        var tree = _s.Build("r")!;
        Assert.AreEqual(0, tree.Root.Children.Count);
        CollectionAssert.AreEqual(new[] { "r" }, Ids(tree.Root).ToArray());
    }

    [TestMethod]
    public void Tree_CousinMarriage_MetAgainBecomesSeeAboveStub_AndIsNotExpandedTwice()
    {
        _g.Add("r");
        _g.Add("x", father: "r", birth: 1);
        _g.Add("y", father: "r", birth: 2);
        _g.Add("xc", father: "x", birth: 3);
        _g.Add("yc", father: "y", female: true, birth: 4);
        _g.Marry("xc", "yc");
        _g.Add("kid", father: "xc", mother: "yc", birth: 5);
        var tree = _s.Build("r")!;
        var cards = AllCards(tree.Root).ToList();
        Assert.AreEqual(1, cards.Count(c => c.HeroId == "kid" && !c.IsRepeat), "the child of the cousins is drawn once");
        Assert.IsTrue(cards.Any(c => c.IsRepeat), "a hero met again is a stub");
        Assert.IsTrue(cards.Where(c => c.IsRepeat).All(c => cards.Any(o => o.HeroId == c.HeroId && !o.IsRepeat)));
    }

    private static IEnumerable<FamilyTreeCard> AllCards(FamilyTreeNode n)
    {
        foreach (var m in n.Members) yield return m;
        foreach (var c in n.Children)
            foreach (var m in AllCards(c)) yield return m;
    }

    private static IEnumerable<FamilyTreeNode> AllNodes(FamilyTreeNode n)
    {
        yield return n;
        foreach (var c in n.Children)
            foreach (var m in AllNodes(c)) yield return m;
    }

    [TestMethod]
    public void Tree_GenerationCap_CutsAtTwelveGenerations_WithACountStub()
    {
        _g.Add("g0");
        for (var i = 1; i <= 15; i++)
            _g.Add("g" + i, father: "g" + (i - 1), birth: i);
        var tree = _s.Build("g0")!;
        var heroNodes = AllNodes(tree.Root).Where(n => n.Members.Count > 0).ToList();
        Assert.AreEqual(FamilyTreeService.MaxGenerations, heroNodes.Count);
        var more = AllNodes(tree.Root).Single(n => n.MoreCount > 0);
        Assert.AreEqual(1, more.MoreCount);
        Assert.AreEqual(0, more.Members.Count);
    }

    [TestMethod]
    public void Tree_CardCap_StopsAt300_WithACountOfTheRest()
    {
        _g.Add("r");
        for (var i = 0; i < 350; i++)
            _g.Add("c" + i.ToString("D3"), father: "r", birth: i);
        var tree = _s.Build("r")!;
        Assert.AreEqual(FamilyTreeService.MaxCards, tree.CardCount);
        Assert.AreEqual(FamilyTreeService.MaxCards, AllCards(tree.Root).Count(c => !c.IsRepeat));
        var more = AllNodes(tree.Root).Single(n => n.MoreCount > 0);
        Assert.AreEqual(351 - FamilyTreeService.MaxCards, more.MoreCount);
    }

    [TestMethod]
    public void Tree_SelectedHeroCardIsMarked_AndOthersCarryAPhrase()
    {
        _g.Add("f");
        _g.Add("a", father: "f");
        var tree = _s.Build("a")!;
        var cards = AllCards(tree.Root).ToList();
        Assert.IsTrue(cards.Single(c => c.HeroId == "a").IsSelected);
        var father = cards.Single(c => c.HeroId == "f");
        Assert.AreEqual(RelationKind.Father, father.Phrase!.Steps[0].Kind);
        Assert.AreEqual("a", father.Phrase.Steps[0].TargetId);
    }

    [TestMethod]
    public void Tree_FarCardsAreKin_WhenBeyondTheHopCap()
    {
        _g.Add("g0");
        for (var i = 1; i <= 9; i++)
            _g.Add("g" + i, father: "g" + (i - 1), birth: i);
        var tree = _s.Build("g9")!;
        var top = AllCards(tree.Root).Single(c => c.HeroId == "g0");
        Assert.IsTrue(top.Phrase!.IsKin);
        Assert.AreEqual("g9", top.Phrase.KinOfId);
        var near = AllCards(tree.Root).Single(c => c.HeroId == "g8");
        Assert.IsFalse(near.Phrase!.IsKin);
    }

    // ---- relation ---------------------------------------------------------------------------------------------

    private static string Words(RelationPhrase? p)
        => p == null ? "null" : string.Join(",", p.Steps.Select(s => s.Kind + ">" + s.TargetId
            + (s.IsTwoParent ? "(" + s.FatherId + "&" + s.MotherId + ")" : "")));

    [TestMethod]
    public void Relation_EveryWord_FollowsTheSubjectsGender()
    {
        _g.Add("b");
        _g.Add("fa", father: null);
        _g.Add("mo", female: true);
        _g.Add("son", father: "b");
        _g.Add("dau", father: "b", female: true);
        _g.Add("hus");
        _g.Add("wif", female: true);
        _g.Add("bro", female: false);
        _g.Add("sis", female: true);
        _g.Add("exh");
        _g.Add("exw", female: true);
        _g.People["b"].Father = "fa"; _g.People["fa"].Kids.Add("b");
        _g.People["b"].Mother = "mo"; _g.People["mo"].Kids.Add("b");
        _g.People["bro"].Father = "fa"; _g.People["fa"].Kids.Add("bro");
        _g.People["sis"].Father = "fa"; _g.People["fa"].Kids.Add("sis");
        _g.Marry("b", "wif");
        _g.People["b"].Ex.Add("exw");
        _g.People["exw"].Ex.Add("b");
        Assert.AreEqual("Father>b", Words(_s.Describe("fa", "b")));
        Assert.AreEqual("Mother>b", Words(_s.Describe("mo", "b")));
        Assert.AreEqual("Son>b", Words(_s.Describe("son", "b")));
        Assert.AreEqual("Daughter>b", Words(_s.Describe("dau", "b")));
        Assert.AreEqual("Wife>b", Words(_s.Describe("wif", "b")));
        Assert.AreEqual("Brother>b", Words(_s.Describe("bro", "b")));
        Assert.AreEqual("Sister>b", Words(_s.Describe("sis", "b")));
        Assert.AreEqual("ExWife>b", Words(_s.Describe("exw", "b")));

        var g2 = new FakeFamilyGraph();
        g2.Add("b", female: true);
        g2.Add("h");
        g2.Add("xh");
        g2.Marry("b", "h");
        g2.People["b"].Ex.Add("xh");
        g2.People["xh"].Ex.Add("b");
        var s2 = new FamilyTreeService(g2);
        Assert.AreEqual("Husband>b", Words(s2.Describe("h", "b")));
        Assert.AreEqual("ExHusband>b", Words(s2.Describe("xh", "b")));
    }

    [TestMethod]
    public void Relation_LastChildToParentStep_NamesBothParents_WhenBothAreKnown()
    {
        _g.Add("dad");
        _g.Add("mum", female: true);
        _g.Add("kid", female: true, father: "dad", mother: "mum");
        Assert.AreEqual("Daughter>dad(dad&mum)", Words(_s.Describe("kid", "dad")));
    }

    [TestMethod]
    public void Relation_ChildWithOneKnownParent_UsesTheSingleForm()
    {
        _g.Add("dad");
        _g.Add("kid", father: "dad");
        Assert.AreEqual("Son>dad", Words(_s.Describe("kid", "dad")));
    }

    [TestMethod]
    public void Relation_HusbandOfZ_DaughterOfW_MiddleStepNamesOneParent()
    {
        _g.Add("w");
        _g.Add("v", female: true);
        _g.Add("z", female: true, father: "w", mother: "v");
        _g.Add("x");
        _g.Marry("x", "z");
        Assert.AreEqual("Husband>z,Daughter>w(w&v)", Words(_s.Describe("x", "w")));
    }

    [TestMethod]
    public void Relation_MiddleChildStep_NamesOnlyOneParent()
    {
        _g.Add("w");
        _g.Add("v", female: true);
        _g.Add("z", father: "w", mother: "v");
        _g.Add("y", father: "z");
        _g.Add("q", father: "y");
        var p = _s.Describe("q", "w")!;
        Assert.AreEqual("Son>y,Son>z,Son>w(w&v)", Words(p));
    }

    [TestMethod]
    public void Relation_ShortestPathWins()
    {
        _g.Add("a");
        _g.Add("b", father: "a");
        _g.Add("c", father: "b");
        _g.Add("t");
        _g.Marry("t", "c");
        _g.People["t"].Female = true;
        _g.People["t"].Father = "a";
        _g.People["a"].Kids.Add("t");
        var p = _s.Describe("t", "a")!;
        Assert.AreEqual(1, p.Steps.Count);
    }

    [TestMethod]
    public void Relation_TiesAreDeterministic_FatherBeforeMother()
    {
        _g.Add("fa");
        _g.Add("mo", female: true);
        _g.Add("x", father: "fa", mother: "mo");
        _g.Add("y", father: "fa", mother: "mo");
        var first = Words(_s.Describe("y", "x"));
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(first, Words(new FamilyTreeService(_g).Describe("y", "x")));
        Assert.AreEqual("Brother>x", first);
    }

    [TestMethod]
    public void Relation_BeyondTheHopCap_IsNull()
    {
        _g.Add("g0");
        for (var i = 1; i <= 8; i++)
            _g.Add("g" + i, father: "g" + (i - 1), birth: i);
        Assert.IsNotNull(_s.Describe("g6", "g0"));
        Assert.IsNull(_s.Describe("g7", "g0"));
    }

    [TestMethod]
    public void Relation_SelfParentAndMutualParentCycles_Terminate()
    {
        _g.Add("a");
        _g.People["a"].Father = "a";
        _g.People["a"].Kids.Add("a");
        _g.Add("b");
        _g.Add("c", father: "b");
        _g.People["b"].Father = "c";
        _g.People["c"].Kids.Add("b");
        Assert.IsNull(_s.Describe("a", "a"));
        Assert.IsNotNull(_s.Describe("c", "b"));
        Assert.IsNull(_s.Describe("a", "b"));
    }

    [TestMethod]
    public void Relation_Unreachable_IsNull()
    {
        _g.Add("a");
        _g.Add("b");
        Assert.IsNull(_s.Describe("a", "b"));
        Assert.IsNull(_s.Describe("nobody", "a"));
    }

    // ---- review fixes ------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tree_CardCap_CoupleRowsNeverExceed300_AndTheOmittedRowsAreCounted()
    {
        _g.Add("r");
        for (var i = 0; i < 150; i++)
        {
            var id = "c" + i.ToString("D3");
            _g.Add(id, father: "r", birth: i);
            _g.Add("s" + i.ToString("D3"), female: true);
            _g.Marry(id, "s" + i.ToString("D3"));
        }

        var tree = _s.Build("r")!;
        var drawn = AllCards(tree.Root).Count(c => !c.IsRepeat);
        Assert.IsTrue(drawn <= FamilyTreeService.MaxCards, "drawn " + drawn);
        Assert.AreEqual(drawn, tree.CardCount);
        var more = AllNodes(tree.Root).Where(n => n.MoreCount > 0).Sum(n => n.MoreCount);
        var rowsDrawn = AllNodes(tree.Root).Count(n => n.Members.Count > 0) - 1;
        Assert.AreEqual(150, rowsDrawn + more);
    }

    [TestMethod]
    public void Tree_CousinMarriage_EveryHeroHasExactlyOneRealCard_AndCardCountCountsEachOnce()
    {
        _g.Add("r");
        _g.Add("x", father: "r", birth: 1);
        _g.Add("y", father: "r", birth: 2);
        _g.Add("xc", father: "x", birth: 3);
        _g.Add("yc", father: "y", female: true, birth: 4);
        _g.Marry("xc", "yc");
        _g.Add("kid", father: "xc", mother: "yc", birth: 5);
        var tree = _s.Build("r")!;
        var cards = AllCards(tree.Root).ToList();
        foreach (var group in cards.GroupBy(c => c.HeroId))
            Assert.AreEqual(1, group.Count(c => !c.IsRepeat), group.Key + " has one real card");
        Assert.AreEqual(cards.Select(c => c.HeroId).Distinct().Count(), tree.CardCount);
    }

    [TestMethod]
    public void Tree_SelectedHeroIsDrawnEvenWhenTheClimbExceedsTheGenerationCap()
    {
        _g.Add("g0");
        for (var i = 1; i <= 14; i++)
            _g.Add("g" + i, father: "g" + (i - 1), birth: i);
        var tree = _s.Build("g13")!;
        Assert.IsTrue(AllCards(tree.Root).Any(c => c.HeroId == "g13" && c.IsSelected && !c.IsRepeat));
    }

    [TestMethod]
    public void Tree_SelectedHeroIsDrawnEvenWhenTheCardCapIsReachedFirst()
    {
        _g.Add("r");
        for (var i = 0; i < 350; i++)
            _g.Add("c" + i.ToString("D3"), father: "r", birth: i);
        var tree = _s.Build("c349")!;
        Assert.IsTrue(AllCards(tree.Root).Any(c => c.HeroId == "c349" && c.IsSelected && !c.IsRepeat));
        Assert.IsTrue(tree.CardCount <= FamilyTreeService.MaxCards);
    }

    [TestMethod]
    public void Relation_AStaleChildEntry_IsNotARelation()
    {
        _g.Add("r");
        _g.Add("o");
        _g.Add("c", father: "o");
        _g.People["r"].Kids.Add("c");
        Assert.IsNull(_s.Describe("c", "r"));
    }

    [TestMethod]
    public void Relation_AStaleSiblingEntry_IsNotARelation()
    {
        _g.Add("p");
        _g.Add("q");
        _g.Add("a", father: "p");
        _g.Add("s", father: "q");
        _g.People["p"].Kids.Add("s");
        Assert.IsNull(_s.Describe("s", "a"));
    }

    [TestMethod]
    public void Relation_TheSearchStopsAtTheVisitedCapInsideOneFanOut()
    {
        _g.Add("r");
        for (var i = 0; i < 50; i++)
            _g.Add("c" + i.ToString("D2"), father: "r", birth: i);
        var finder = new RelationPathFinder(_g, "r", 10);
        Assert.IsTrue(finder.VisitedCount <= 10, "visited " + finder.VisitedCount);
    }

    [TestMethod]
    public void Relation_EqualLengthPaths_ChooseByBirthOrderNotListOrder()
    {
        _g.Add("b");
        _g.Add("o", birth: 1, father: "b");
        _g.Add("y", birth: 2, father: "b", female: true);
        _g.Add("w", birth: 3, father: "o", mother: "y");
        _g.People["b"].Kids.Reverse();
        var phrase = _s.Describe("w", "b")!;
        Assert.AreEqual("o", phrase.Steps[0].TargetId);
    }
}
