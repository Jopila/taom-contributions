using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BanditVoices;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BanditVoices;

/// <summary>
/// #900: the shipped rows against what vanilla reads. Vanilla's robbery code asks for a threat by
/// <c>MapFaction.StringId + "_" + k</c> and then FINDS the other three lines under the same variation with no fallback, so a
/// variant missing one of the four shows "ERROR: Text with id ... doesn't exist" in the middle of a robbery.
/// The loader splits an id on ".", so a variation holding a dot would silently lose its tail.
/// </summary>
[TestClass]
public class BanditVoicesDataTests
{
    private static readonly string[] Talking =
    {
        "dunland_raiders", "rhun_raiders", "harad_raiders", "gundabad_raiders",
        "umbar_corsairs", "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers",
    };

    private static readonly string[] RobberyKinds = { "threat", "pay_agreement", "conclusion", "start_fight" };

    private static readonly Lazy<List<(string Id, string Text)>> Rows = new(() =>
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_bandit_voice_strings.xml"));
        return doc.Descendants("string")
            .Select(e => ((string)e.Attribute("id")!, (string)e.Attribute("text")!))
            .ToList();
    });

    private static readonly Regex Keyed = new(@"^\{=(?<key>[^}]+)\}(?<body>.*)$", RegexOptions.Singleline);

    [TestMethod]
    public void TheFile_HoldsSixtyFourRobberyRows_AndSixteenGreetingRows_AndNothingElse()
    {
        Assert.AreEqual(64, Rows.Value.Count(r => r.Id.StartsWith("str_robbery_", StringComparison.Ordinal)));
        Assert.AreEqual(16, Rows.Value.Count(r => r.Id.StartsWith("str_taom_bv_greeting.", StringComparison.Ordinal)));
        Assert.AreEqual(80, Rows.Value.Count);
    }

    [TestMethod]
    public void EveryRobberyVariant_HasAllFourRows_UnderTheSameVariationId()
    {
        foreach (var clan in Talking)
        {
            foreach (var k in new[] { 1, 2 })
            {
                foreach (var kind in RobberyKinds)
                {
                    var id = $"str_robbery_{kind}.{clan}_{k}";
                    Assert.AreEqual(1, Rows.Value.Count(r => r.Id == id), "exactly one row for " + id);
                }
            }
        }
    }

    [TestMethod]
    public void NoRobberyVariant_ExistsBeyondTheTwoPerClan()
    {
        var expected = new HashSet<string>(
            from clan in Talking from k in new[] { 1, 2 } from kind in RobberyKinds select $"str_robbery_{kind}.{clan}_{k}",
            StringComparer.Ordinal);
        var actual = Rows.Value.Where(r => r.Id.StartsWith("str_robbery_", StringComparison.Ordinal)).Select(r => r.Id);

        CollectionAssert.AreEquivalent(expected.ToList(), actual.ToList());
    }

    [TestMethod]
    public void EachTalkingClan_HasBothGreetingVariations()
    {
        foreach (var clan in Talking)
        {
            foreach (var kind in new[] { "harmless", "brothers" })
                Assert.AreEqual(1, Rows.Value.Count(r => r.Id == $"str_taom_bv_greeting.{clan}_{kind}"), clan + " " + kind);
        }
    }

    [TestMethod]
    public void SpidersTrollsAndLooters_HaveNoRows()
    {
        foreach (var quiet in new[] { "mirkwood_spiders", "wild_trolls", "looters" })
            Assert.IsFalse(Rows.Value.Any(r => r.Id.Contains(quiet)), quiet + " must keep vanilla's silence");
    }

    [TestMethod]
    public void TheEightClans_AreBanditFactionsInTheShippedClanData_AndTheCreatureClansAreToo()
    {
        var doc = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "characters", "clans.xml"));
        var bandits = doc.Descendants("Faction")
            .Where(f => (string?)f.Attribute("is_bandit") == "true")
            .Select(f => (string)f.Attribute("id")!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var clan in Talking)
            Assert.IsTrue(bandits.Contains(clan), clan + " is not a bandit Faction in clans.xml");
        Assert.IsTrue(bandits.Contains("mirkwood_spiders") && bandits.Contains("wild_trolls"), "the creature clans moved: revisit the exclusion");
    }

    [TestMethod]
    public void EveryId_HasExactlyOneDot_SoTheLoaderKeepsTheWholeVariation()
    {
        foreach (var (id, _) in Rows.Value)
        {
            var parts = id.Split('.');
            Assert.AreEqual(2, parts.Length, id + " must be <textId>.<variation> with no further dot");
            Assert.IsTrue(parts[0] == "str_taom_bv_greeting" || parts[0].StartsWith("str_robbery_", StringComparison.Ordinal), id);
            Assert.IsTrue(parts[1].Length > 0 && !parts[1].Contains(' '), id);
        }
    }

    [TestMethod]
    public void EveryRow_CarriesItsOwnTaomKey_UsedOnce_AndNeverAVanillaOne()
    {
        var keys = new List<string>();
        foreach (var (id, text) in Rows.Value)
        {
            var m = Keyed.Match(text);
            Assert.IsTrue(m.Success, id + " must be {=KEY}English");
            var key = m.Groups["key"].Value;
            StringAssert.StartsWith(key, "taom_bv_", id + " key");
            Assert.IsTrue(m.Groups["body"].Value.Trim().Length > 10, id + " has no English");
            keys.Add(key);
        }

        Assert.AreEqual(keys.Count, keys.Distinct(StringComparer.Ordinal).Count(), "a key shared by two rows shows one clan's words for both");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void NoRowKeyOrId_IsOneVanillaAlreadyDefines()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var vanilla = Path.Combine(game, "Modules", "Native", "ModuleData", "module_strings.xml");
        if (!File.Exists(vanilla))
            Assert.Inconclusive("Vanilla module_strings.xml not found at " + vanilla);

        var vanillaRows = XDocument.Load(vanilla).Descendants("string")
            .Select(e => ((string?)e.Attribute("id") ?? "", (string?)e.Attribute("text") ?? ""))
            .ToList();
        var vanillaIds = vanillaRows.Select(r => r.Item1).ToHashSet(StringComparer.Ordinal);
        var vanillaKeys = vanillaRows.Select(r => Keyed.Match(r.Item2)).Where(m => m.Success).Select(m => m.Groups["key"].Value).ToHashSet(StringComparer.Ordinal);

        foreach (var (id, text) in Rows.Value)
        {
            Assert.IsFalse(vanillaIds.Contains(id), id + " is a vanilla row id");
            Assert.IsFalse(vanillaKeys.Contains(Keyed.Match(text).Groups["key"].Value), id + " reuses a vanilla key");
        }
    }

    [TestMethod]
    public void NoOtherShippedStringsFile_UsesAnyBanditVoicesKey()
    {
        var dir = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var file in Directory.EnumerateFiles(dir, "*strings*.xml", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(file) == "taom_bandit_voice_strings.xml")
                continue;
            Assert.IsFalse(File.ReadAllText(file).Contains("{=taom_bv_"), Path.GetFileName(file) + " carries a taom_bv_ key");
        }
    }

    [TestMethod]
    public void TheEnglish_HasNoMaleOnlyAddress_NoPlaceholder_AndNoDash()
    {
        var male = new Regex(@"\b(lads?|boys?|sir|sirs|brothers?|gentlemen|fellows?|sons?|he|him|his|woman|women|lady|ladies|she|her|hers|sisters?|girls?|lass|lasses|maids?|(?!human)[\w-]*(?:man|men))\b", RegexOptions.IgnoreCase);
        foreach (var (id, text) in Rows.Value)
        {
            var body = Keyed.Match(text).Groups["body"].Value;
            Assert.IsFalse(male.IsMatch(body), id + " addresses or names a gender: " + body);
            Assert.IsFalse(body.Contains('{') || body.Contains('}'), id + " carries a placeholder vanilla does not fill for this line");
            Assert.IsFalse(body.Contains('\u2014') || body.Contains('\u2013'), id + " has an em or en dash");
        }
    }

    [TestMethod]
    public void NoLine_NamesTheListenersPeople()
    {
        // The player can be a dwarf, an orc or an elf: a line may insult or place the listener, never by race or build.
        var outsider = new Regex(
            @"-folk\b|\bso (?:tall|short|small|big)\b|soft-?skin|\w+landers?\b|\w+erners?\b|\b(?:green|western|eastern|northern|southern) lands\b|\b(?:your|you) (?:kind|people|race|folk)\b|\blandsm[ae]n\b",
            RegexOptions.IgnoreCase);
        foreach (var (id, text) in Rows.Value)
            Assert.IsFalse(outsider.IsMatch(text), id + " names the listener's people: " + text);
    }

    [TestMethod]
    public void TheTagsInsideRows_AreOnlyTheOnesVanillaRowsUse()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "rf:convo_mocking_revenge", "if:convo_mocking_revenge", "if:convo_bared_teeth",
            "if:convo_angry", "rf:convo_angry", "rf:convo_bemused", "if:convo_annoyed", "if:convo_very_stern",
            "if:convo_insulted", "ib:aggressive", "ib:hip", "ib:closed",
        };
        foreach (var (id, text) in Rows.Value)
        {
            foreach (Match tag in Regex.Matches(Keyed.Match(text).Groups["body"].Value, @"\[([^\]]*)\]"))
                Assert.IsTrue(allowed.Contains(tag.Groups[1].Value), id + " uses tag [" + tag.Groups[1].Value + "]");
        }
    }
}
