using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// #805: TAOM installed KEYforce's Harad regular weapons (lotraom-assets db981663) with three edits his
// files do not carry: the spear pieces in TwoHandedPolearm_Couchable, the three longbows off the shelves,
// and English rows for his item and piece names. The Armory is UNVERSIONED, so his next drop or a reinstall
// reverts all three silently (docs/reference/lotrlome-armory-snapshot/README.md, "APPLIED EDIT: KEYforce's
// Harad regular weapons"). The live tests are Inconclusive where the Armory is not installed.

namespace TAOM.Tests.Features.ArmourAcquisition;

[TestClass]
public class HaradWeaponsLiveEditTests
{
    private static readonly string[] Longbows = { "hr_st_longbow_light", "hr_st_longbow_med", "hr_st_longbow_heavy" };
    private static readonly Regex HaradPiecePrefix = new Regex("^(sm|sk)_(hr|sg)_st_", RegexOptions.Compiled);

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string path = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!File.Exists(Path.Combine(path, "LOTRLOME_items", "harad", "weapons.xml")))
            Assert.Inconclusive("LOTRLOME_Armory with harad/weapons.xml is not installed here; the live edits cannot be checked.");
        return path;
    }

    private static XDocument HaradWeapons(string md) =>
        XDocument.Load(Path.Combine(md, "LOTRLOME_items", "harad", "weapons.xml"));

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveHaradSpears_EveryPiece_IsListedInCouchable()
    {
        string md = ArmoryModuleData();
        var spearPieces = HaradWeapons(md).Descendants("CraftedItem")
            .Where(c => (string?)c.Attribute("crafting_template") == "TwoHandedPolearm")
            .SelectMany(c => c.Descendants("Piece").Select(p => (string?)p.Attribute("id") ?? ""))
            .Distinct().ToList();
        Assert.IsTrue(spearPieces.Count >= 6, $"only {spearPieces.Count} Harad polearm pieces found; the parse is broken");

        string xslt = File.ReadAllText(Path.Combine(md, "weapon_descriptions.xslt"));
        var block = Regex.Match(xslt,
            @"WeaponDescription\[@id='TwoHandedPolearm_Couchable'\]/AvailablePieces.*?</xsl:template>",
            RegexOptions.Singleline);
        Assert.IsTrue(block.Success, "the TwoHandedPolearm_Couchable template is missing from weapon_descriptions.xslt");
        var couchable = new HashSet<string>(
            Regex.Matches(block.Value, "<AvailablePiece id=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value));

        var missing = spearPieces.Where(p => !couchable.Contains(p)).ToList();
        Assert.AreEqual(0, missing.Count,
            "Harad spear pieces missing from TwoHandedPolearm_Couchable, so those spears cannot couch "
            + "(KEYforce's db981663 left them out; restore the TAOM-HARAD-COUCH block): " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveHaradLongbows_AreNotMerchandise()
    {
        var items = HaradWeapons(ArmoryModuleData()).Descendants("Item")
            .ToDictionary(i => (string?)i.Attribute("id") ?? "", i => i);
        foreach (string id in Longbows)
        {
            Assert.IsTrue(items.ContainsKey(id), $"{id} is missing from harad/weapons.xml");
            Assert.AreEqual("false", (string?)items[id].Attribute("is_merchandise"),
                $"{id} is on sale again: it needs Bow {(string?)items[id].Attribute("difficulty")}, which a buyer "
                + "cannot meet (KEYforce's db981663 ships it as merchandise)");
        }
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveHaradWeaponAndPieceNames_HaveEnglishRows()
    {
        string md = ArmoryModuleData();
        var registered = new HashSet<string>(Directory.GetFiles(Path.Combine(md, "Languages"), "*.xml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "<string\\s+id=\"([^\"]+)\"").Cast<Match>())
            .Select(m => m.Groups[1].Value));

        var keyed = HaradWeapons(md).Root!.Elements()
            .Select(e => (string?)e.Attribute("name") ?? "")
            .Concat(XDocument.Load(Path.Combine(md, "LOTRLOME_crafting_pieces.xml")).Descendants("CraftingPiece")
                .Where(p => HaradPiecePrefix.IsMatch((string?)p.Attribute("id") ?? ""))
                .Select(p => (string?)p.Attribute("name") ?? ""))
            .Select(n => Regex.Match(n, @"^\{=([^}]+)\}"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct().ToList();
        Assert.IsTrue(keyed.Count >= 50, $"only {keyed.Count} Harad weapon and piece name keys found; the parse is broken");

        var unregistered = keyed.Where(k => !registered.Contains(k)).ToList();
        Assert.AreEqual(0, unregistered.Count,
            "Harad weapon and piece names with no English row in the Armory's Languages/*.xml, so every language "
            + "shows the inline English with no error: " + string.Join(", ", unregistered));
    }
}
