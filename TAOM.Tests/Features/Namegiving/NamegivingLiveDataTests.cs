using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Namegiving;

namespace TAOM.Tests.Features.Namegiving;

/// <summary>
/// The installed files Namegiving depends on: the vanilla Fiefs panel the Rename button is inserted into, and the live,
/// unversioned TAOM_Map settlements the four lore names belong to. Inconclusive on a machine without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class NamegivingLiveDataTests
{
    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static string Installed(params string[] parts)
    {
        var path = Path.Combine(new[] { GameDir() }.Concat(parts).ToArray());
        if (!File.Exists(path))
            Assert.Inconclusive(path + " is not installed on this machine; the live data cannot be checked here.");
        return path;
    }

    [TestMethod]
    public void TheXPath_MatchesExactlyOneNode_InTheInstalledFiefsPanel()
    {
        var doc = new XmlDocument();
        doc.Load(Installed("Modules", "SandBox", "GUI", "Prefabs", "KingdomManagement", "Fiefs", "FiefsPanel.xml"));

        var matches = doc.DocumentElement!.SelectNodes("descendant::ListPanel[@Id='ActionButtonsContainer']/Children")!;

        Assert.AreEqual(1, matches.Count, "the Rename block is inserted as the last child of the one action column; a second match would add a button to each");
    }

    [TestMethod]
    public void TheFiefsPanel_IsMountedWithTheSettlementViewModelAsItsDataSource()
    {
        var doc = new XmlDocument();
        doc.Load(Installed("Modules", "SandBox", "GUI", "Prefabs", "KingdomManagement", "KingdomManagement.xml"));

        var panel = doc.SelectSingleNode("//FiefsPanel[@Id='FiefsPanel']") as XmlElement;

        Assert.IsNotNull(panel);
        Assert.AreEqual("{Settlement}", panel.GetAttribute("DataSource"), "the block's {..} steps from the Settlement view model up to the KingdomManagementVM");
    }

    [TestMethod]
    public void EveryLoreSettlement_ExistsInTheLiveMap_AndNoPlaceAlreadyBearsALoreName()
    {
        var root = XDocument.Load(Installed("Modules", "TAOM_Map", "ModuleData", "settlements.xml")).Root!;
        var settlements = root.Elements("Settlement").ToList();
        var ids = settlements.Select(s => (string?)s.Attribute("id")).ToList();
        var names = settlements.Select(s => PlainName((string?)s.Attribute("name") ?? "")).ToList();

        foreach (var row in LoreNames.All)
        {
            CollectionAssert.Contains(ids, row.SettlementId, row.SettlementId + " is not a settlement id in the live TAOM_Map");
            Assert.IsFalse(names.Any(n => string.Equals(n, row.English, StringComparison.InvariantCultureIgnoreCase)),
                "a settlement already bears the lore name " + row.English + ", so the lore pass would skip it forever");
        }
    }

    // A data name is "{=key}Text" or plain text.
    private static string PlainName(string name) => name.StartsWith("{=", StringComparison.Ordinal) ? name.Substring(name.IndexOf('}') + 1) : name;
}
