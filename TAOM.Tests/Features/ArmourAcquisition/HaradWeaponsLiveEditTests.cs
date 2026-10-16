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
// Harad regular weapons"). His 03320a65 adds javelins, maces and the regular-line bardings and saddles,
// again with no English rows, and put his javelin pieces inside a TAOM-STARTER-KIT block that
// generate_starter_kit.py rewrites. The live tests are Inconclusive where the Armory is not installed.

namespace TAOM.Tests.Features.ArmourAcquisition;

[TestClass]
[TestCategory("LiveInstall")]
public class HaradWeaponsLiveEditTests
{
    private static readonly string[] Longbows = { "hr_st_longbow_light", "hr_st_longbow_med", "hr_st_longbow_heavy" };
    private static readonly Regex HaradRegularPrefix = new Regex("^(sm|sk)_(hr|sg)_st_", RegexOptions.Compiled);

    // The weapon descriptions a TAOM edit or a TAOM tool puts at risk, by crafting template. A crafted item
    // gains a usage only from a description that lists every one of its pieces.
    private static readonly (string Template, int MinPieces, string Description, string Why)[] NeededDescriptions =
    {
        ("TwoHandedPolearm", 6, "TwoHandedPolearm_Couchable", "the spears cannot couch (restore the TAOM-HARAD-COUCH block)"),
        ("Javelin", 3, "Javelin", "the javelins cannot be thrown"),
        ("Javelin", 3, "OneHandedPolearm_JavelinAlternative", "the javelins lose their one-handed melee mode"),
        ("Mace", 3, "Mace", "the maces do not load"),
    };

    // generate_starter_kit.py rewrites these blocks on --apply and deletes them on --revert.
    private static readonly Regex StarterKitBlock = new Regex(
        @"<!-- TAOM-STARTER-KIT:START -->.*?<!-- TAOM-STARTER-KIT:END -->", RegexOptions.Singleline);

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

    private static HashSet<string> RegisteredKeys(string md) =>
        new HashSet<string>(Directory.GetFiles(Path.Combine(md, "Languages"), "*.xml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "<string\\s+id=\"([^\"]+)\"").Cast<Match>())
            .Select(m => m.Groups[1].Value));

    private static List<string> Keys(IEnumerable<string> names) =>
        names.Select(n => Regex.Match(n, @"^\{=([^}]+)\}")).Where(m => m.Success)
            .Select(m => m.Groups[1].Value).Distinct().ToList();

    [TestMethod]
    public void LiveHaradWeapons_EveryPiece_IsListedInEveryNeededDescription_OutsideStarterKitBlocks()
    {
        string md = ArmoryModuleData();
        var crafted = HaradWeapons(md).Descendants("CraftedItem").ToList();
        // Parsed, not regex-matched: a commented-out AvailablePiece must not count as listed.
        var stylesheet = XDocument.Parse(
            StarterKitBlock.Replace(File.ReadAllText(Path.Combine(md, "weapon_descriptions.xslt")), ""));
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";

        var missing = new List<string>();
        foreach (var (template, minPieces, description, why) in NeededDescriptions)
        {
            var pieces = crafted
                .Where(c => (string?)c.Attribute("crafting_template") == template)
                .SelectMany(c => c.Descendants("Piece").Select(p => (string?)p.Attribute("id") ?? ""))
                .Distinct().ToList();
            Assert.IsTrue(pieces.Count >= minPieces,
                $"only {pieces.Count} Harad {template} pieces found; the parse is broken, or a drop removed his {template} items");

            string match = $"WeaponDescription[@id='{description}']/AvailablePieces";
            var templates = stylesheet.Descendants(xsl + "template")
                .Where(t => (string?)t.Attribute("match") == match).ToList();
            Assert.IsTrue(templates.Count <= 1,
                $"{templates.Count} templates match {match}; only one can apply, so reading any one proves nothing");
            var listed = new HashSet<string>(templates
                .SelectMany(t => t.Descendants("AvailablePiece").Select(p => (string?)p.Attribute("id") ?? "")));
            missing.AddRange(pieces.Where(p => !listed.Contains(p)).Select(p => $"{p} not in {description} ({why})"));
        }

        Assert.AreEqual(0, missing.Count,
            "Harad weapon pieces missing from a weapon description they need, once the TAOM-STARTER-KIT blocks "
            + "(which generate_starter_kit.py rewrites and deletes) are set aside: " + string.Join("; ", missing));
    }

    [TestMethod]
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
    public void LiveHaradWeaponPieceAndHarnessNames_HaveEnglishRows()
    {
        string md = ArmoryModuleData();
        var registered = RegisteredKeys(md);

        var weaponAndPieceKeys = Keys(HaradWeapons(md).Root!.Elements()
            .Select(e => (string?)e.Attribute("name") ?? "")
            .Concat(XDocument.Load(Path.Combine(md, "LOTRLOME_crafting_pieces.xml")).Descendants("CraftingPiece")
                .Where(p => HaradRegularPrefix.IsMatch((string?)p.Attribute("id") ?? ""))
                .Select(p => (string?)p.Attribute("name") ?? "")));
        var harnessKeys = Keys(XDocument.Load(Path.Combine(md, "LOTRLOME_items", "harad", "body_armors.xml")).Descendants("Item")
            .Where(i => (string?)i.Attribute("Type") == "HorseHarness"
                && HaradRegularPrefix.IsMatch((string?)i.Attribute("id") ?? ""))
            .Select(i => (string?)i.Attribute("name") ?? ""));
        Assert.IsTrue(weaponAndPieceKeys.Count >= 50,
            $"only {weaponAndPieceKeys.Count} Harad weapon and piece name keys found; the parse is broken");
        Assert.IsTrue(harnessKeys.Count >= 12,
            $"only {harnessKeys.Count} Harad regular barding and saddle name keys found; the parse is broken, or a drop moved or renamed them");

        var keyed = weaponAndPieceKeys.Concat(harnessKeys).Distinct().ToList();

        var unregistered = keyed.Where(k => !registered.Contains(k)).ToList();
        Assert.AreEqual(0, unregistered.Count,
            "Harad weapon, piece and regular-line barding and saddle names with no English row in the Armory's "
            + "Languages/*.xml, so every language shows the inline English with no error: " + string.Join(", ", unregistered));
    }
}
