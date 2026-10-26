using System;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>
/// TAOM's copy of the encyclopedia hero page (#907): the Family block moves up under the clan block, before Allies and Enemies,
/// and the margins are retuned around it. The anchors the Lines of Descent and Messengers extensions use must survive the move.
/// What no test can show is the look; that is the in-game check.
/// </summary>
[TestClass]
public class EncyclopediaHeroPageLayoutTests
{
    private static XmlDocument Load()
    {
        var doc = new XmlDocument();
        doc.Load(RepoPath("Main", "_Module", "GUI", "PreFabs", "Encyclopedia", "EncyclopediaSubPages", "EncyclopediaHeroPage.xml"));
        return doc;
    }

    private static XmlElement ById(XmlDocument doc, string id) => (XmlElement)doc.SelectSingleNode($"//*[@Id='{id}']")!;

    private static XmlElement[] SiblingElements(XmlElement element) =>
        element.ParentNode!.ChildNodes.OfType<XmlElement>().ToArray();

    [TestMethod]
    public void TheFamilyBlock_SitsBetweenTheClanBlockAndTheAlliesDivider_AsOneContiguousRun()
    {
        var doc = Load();
        var siblings = SiblingElements(ById(doc, "FamilyDivider"));
        var ids = siblings.Select(e => e.GetAttribute("Id")).ToList();
        var scope = siblings.Single(e => e.GetAttribute("ScopeID") == "EncyclopediaHeroFamilyContentScope");

        var at = ids.IndexOf("InfoContainer");
        Assert.IsTrue(at >= 0, "InfoContainer is a sibling of the Family block");
        Assert.AreSame(siblings[at + 1], ById(doc, "FamilyDivider"), "the Family divider follows the clan block directly");
        Assert.AreSame(siblings[at + 2], scope, "the targeter follows the divider");
        Assert.AreSame(siblings[at + 3], ById(doc, "FamilyGrid"), "the grid follows the targeter");
        Assert.AreSame(siblings[at + 4], ById(doc, "AlliesDivider"), "Allies starts right after the grid");
        Assert.IsTrue(ids.IndexOf("FamilyGrid") < ids.IndexOf("EnemiesDivider"));
    }

    [TestMethod]
    public void TheFamilyBlock_KeepsItsCommentsInOrder()
    {
        var parent = ById(Load(), "FamilyDivider").ParentNode!;
        var comments = parent.ChildNodes.OfType<XmlComment>().Select(c => c.Value!.Trim()).ToList();
        Assert.IsTrue(comments.IndexOf("Family Divider") >= 0 && comments.IndexOf("Family Grid") > comments.IndexOf("Family Divider"));
        Assert.IsTrue(comments.IndexOf("Family Grid") < comments.IndexOf("Allies Divider"));
    }

    [TestMethod]
    public void TheMargins_AreRetunedAroundTheMovedBlock()
    {
        var doc = Load();
        Assert.AreEqual("30", ById(doc, "FamilyDivider").GetAttribute("MarginTop"));
        Assert.AreEqual("25", ById(doc, "FamilyGrid").GetAttribute("MarginTop"));
        Assert.AreEqual("10", ById(doc, "FamilyGrid").GetAttribute("MarginBottom"));
        Assert.AreEqual("20", ById(doc, "AlliesDivider").GetAttribute("MarginTop"));
        Assert.AreEqual("20", ById(doc, "EnemiesDivider").GetAttribute("MarginTop"));
        Assert.AreEqual("50", ById(doc, "EnemiesGridParent").GetAttribute("MarginBottom"), "the last block of the column keeps the page bottom padding the Family grid had in vanilla");
    }

    [TestMethod]
    public void EveryId_IsUnique()
    {
        var ids = Load().SelectNodes("//*[@Id]")!.Cast<XmlElement>().Select(e => e.GetAttribute("Id")).ToList();
        var duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, duplicates.Count, string.Join(", ", duplicates));
    }

    [TestMethod]
    public void EveryItemListAndScopeParent_ResolvesToASibling()
    {
        var doc = Load();
        var checkedCount = 0;
        foreach (var element in doc.SelectNodes("//*")!.Cast<XmlElement>())
        {
            foreach (var attribute in new[] { "Parameter.ItemList", "ScopeParent" })
            {
                var value = element.GetAttribute(attribute);
                if (!value.StartsWith("..\\", StringComparison.Ordinal))
                    continue;
                checkedCount++;
                var id = value.Substring(3);
                Assert.IsTrue(SiblingElements(element).Any(s => s.GetAttribute("Id") == id),
                    $"<{element.Name} {attribute}=\"{value}\"> names no sibling Id");
            }
        }

        Assert.IsTrue(checkedCount >= 6, "the check found only " + checkedCount + " references; it is not reading the page");
    }

    [TestMethod]
    public void TheLinesOfDescentAnchor_StaysUnique()
    {
        var count = Load().SelectNodes("descendant::NavigationScopeTargeter[@ScopeID='EncyclopediaHeroFamilyContentScope']")!.Count;
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void TheMessengersAnchor_MatchesExactlyOnce()
    {
        var count = Load().SelectNodes("descendant::RichTextWidget[@Text='@InformationText']")!.Count;
        Assert.AreEqual(1, count);
    }
}
