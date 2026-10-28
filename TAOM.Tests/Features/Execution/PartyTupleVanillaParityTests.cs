using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Execution;

/// <summary>Pins TAOM's party tuple copies to the installed vanilla prefabs apart from TAOM's two edits, so a missing vanilla widget such as the Execute button's blood-feud mark fails (#1031).</summary>
[TestClass]
public class PartyTupleVanillaParityTests
{
    [DataTestMethod]
    [TestCategory("LiveInstall")]
    [DataRow("PartyTroopTuple.xml")]
    [DataRow("PartyTroopTupleLeft.xml")]
    public void EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits(string file)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var vanillaPath = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "Party", file);
        if (!File.Exists(vanillaPath))
            Assert.Inconclusive("vanilla prefab not found at " + vanillaPath);

        CollectionAssert.AreEqual(Flatten(vanillaPath), Flatten(RepoPath("Main", "_Module", "GUI", "Prefabs", "Party", file)), file);
    }

    [TestMethod]
    public void TheExecuteButton_ShowsTheBloodFeudMark_BesideTheSkull()
    {
        var doc = new XmlDocument();
        doc.Load(RepoPath("Main", "_Module", "GUI", "Prefabs", "Party", "PartyTroopTuple.xml"));
        var children = doc.SelectSingleNode("//ButtonWidget[@Id='ExecutePrisonerButton']/Children") as XmlElement;
        Assert.IsNotNull(children, "the Execute button is missing from TAOM's tuple copy");
        var names = children!.ChildNodes.OfType<XmlElement>().Select(e => e.GetAttribute("Sprite")).ToList();

        var skull = names.FindIndex(s => s.EndsWith("kill_feed_skull"));
        Assert.IsTrue(skull >= 0, "the Execute button has no skull");
        Assert.AreEqual(@"SPGeneral\blood_feud_icon", names.ElementAtOrDefault(skull + 1));
    }

    // Every element except TAOM's two edits, which their own tests pin: the #902 include (PrisonerBannersUiWiringTests) and the
    // zero transition durations (ShadowfaxScreensPrefabTests).
    private static List<string> Flatten(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc.SelectNodes("//*[not(self::TaomPrisonerBanners)]")!.Cast<XmlElement>()
            .Select(e => e.Name + " " + string.Join(" ", e.Attributes.Cast<XmlAttribute>()
                .Where(a => !(e.Name == "VisualDefinition" && a.Name == "TransitionDuration"))
                .OrderBy(a => a.Name).Select(a => a.Name + "=" + a.Value)))
            .ToList();
    }
}
